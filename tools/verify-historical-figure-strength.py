"""Run DF 53.17 machine code in Unicorn, never in the game process.

Usage: python tools/verify-historical-figure-strength.py PATH_TO_EXE
Dependencies: pefile==2024.8.26 unicorn==2.1.4
No proprietary bytes are distributed. Synthetic fixtures only.
"""
import hashlib
import random
import struct
import sys
from pathlib import Path
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_64
from unicorn.x86_const import *

binary = Path(sys.argv[1])
assert hashlib.sha256(binary.read_bytes()).hexdigest() == '3436f0553d6fa17b25716f8b1f034b52e3d9dc30be7f3ddac67cd0ff731d7b16'
pe = pefile.PE(str(binary))
u = Uc(UC_ARCH_X86, UC_MODE_64)
base = pe.OPTIONAL_HEADER.ImageBase
u.mem_map(base, (pe.OPTIONAL_HEADER.SizeOfImage + 4095) & ~4095)
u.mem_write(base, pe.get_memory_mapped_image())
u.mem_map(0x100000, 0x20000)
hf, info, profile, ids, points, inventory = [0x100000 + n * 0x1000 for n in range(6)]
race, caste, vector, castes, stack, stop = [0x106000 + n * 0x1000 for n in range(6)]
offensive = {37,38,39,40,41,42,43,44,45,49,50,51,52}
defensive = {86,87,99,100,101,102,103}
def put(address, fmt, *values):
    u.mem_write(address, struct.pack('<' + fmt, *values))

def run(skills, rating=120, material=None, profession=0, quality=0, specific=None):
    u.mem_write(0x100000, bytes(0x20000))
    put(hf, 'hhh', profession, 0, 0)
    put(hf + 0x10, 'i', -1)
    put(hf + 0x130, 'Q', info)
    put(hf + 0x170, 'ii', -1, -1)
    put(info + 8, 'Q', profile)
    put(profile, 'QQ', ids, ids + len(skills) * 2)
    put(profile + 0x18, 'Q', points)
    for n, (skill, xp) in enumerate(skills):
        put(ids + n * 2, 'h', skill)
        put(points + n * 4, 'i', xp)
    if material is not None:
        put(info + 0x50, 'Q', inventory)
        put(inventory + 0x18, 'ii', material, quality)
        put(inventory + 0x8c, 'I', 2 if quality else 0)
    if specific is not None:
        put(info + 0x50, 'Q', inventory)
        put(inventory + 0x8c, 'I', 1)
        put(inventory + 0x58, '13i', *specific)
    put(0x14250eee8, 'QQ', vector, vector + 8)
    put(vector, 'Q', race)
    put(race + 0x178, 'QQ', castes, castes + 8)
    put(castes, 'Q', caste)
    put(caste + 0x4d0, 'i', rating)
    sp = stack + 0x808
    put(sp, 'Q', stop)
    put(sp + 0x28, 'H', 0)
    for reg, val in [(UC_X86_REG_RSP,sp),(UC_X86_REG_RCX,hf),(UC_X86_REG_RDX,0),
                     (UC_X86_REG_R8,65535),(UC_X86_REG_R9,65535)]:
        u.reg_write(reg,val)
    u.emu_start(0x140bdc3a0, stop, count=10000)
    assert u.reg_read(UC_X86_REG_RIP) == stop
    return u.reg_read(UC_X86_REG_EAX), struct.unpack('<ii', u.mem_read(hf+0x170,8))

def expected(skills, rating, material, profession=0, quality=0):
    a = sum(xp for skill,xp in skills if skill in offensive)
    d = sum(xp for skill,xp in skills if skill in defensive)
    if material is not None:
        if quality:
            material = max(10,material) * (5+quality) // 5
        rating += material * min(80,40+a//500) // 40
    rating = rating * min(80,40+d//500) // 40
    return rating // (10 if profession==103 else 100 if profession==104 else 1), (a,d)

checks = 0
for skill in range(150):
    for material in (None,10,250):
        args = ([(skill,18000)],120,material)
        assert run(*args) == expected(*args), (args,run(*args),expected(*args))
        checks += 1
rng = random.Random(5317)
for _ in range(600):
    skills = [(s,rng.choice([0,1,499,500,501,17999,18000,19999,20000,29000,100000])) for s in rng.sample(range(150),15)]
    args = (skills,rng.randrange(1,10000),rng.choice([None,10,123,500]),rng.choice([0,103,104]),rng.randrange(6))
    assert run(*args) == expected(*args), (args,run(*args),expected(*args))
    checks += 1
print(f'{checks} native checks passed: skill dispatch, XP thresholds/cap, generic equipment/quality, no equipment, profession divisors.')
slots = [37,38,39,40,41,42,43,49,50,51,52,44,45]
for _ in range(200):
    skills = [(s,rng.choice([0,499,500,18000,20000,29000])) for s in slots + sorted(defensive)]
    specific = [rng.randrange(0,500) for _ in slots]
    rating = rng.randrange(1,10000)
    contributions = [m * min(80,40+dict(skills)[s]//500)//40 for s,m in zip(slots,specific)]
    predicted = (rating + max(contributions[:11]) + sum(contributions[11:])) * min(80,40+sum(dict(skills)[s] for s in defensive)//500)//40
    actual, sums = run(skills,rating,specific=specific)
    assert actual == predicted and sums == (0,sum(dict(skills)[s] for s in defensive)), (actual,predicted,sums)
    checks += 1
print(f'{checks} total native checks passed, including 200 specific-item inventory cases (best weapon plus shield and armor).')
print('Not validated: entity fallback, age-dependent caste helper, live battle simulation.')

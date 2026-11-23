export interface DarkForceFaction {
  id: number;
  name: string;
  fromYear: number;
  throughYear: number | null;
  evidence: string;
}

export interface DarkForceEvent {
  year: number;
  kind: string;
  description: string;
  siteId: number | null;
  siteName: string | null;
  targetEntityId: number | null;
  targetEntityName: string | null;
  isInvasion: boolean;
  isPrelude: boolean;
  attribution: string;
}

export interface DarkForce {
  figureId: number;
  name: string;
  awakeningYear: number | null;
  status: string;
  defeated: boolean;
  defeatYear: number | null;
  peakSettlements: number;
  peakYear: number | null;
  currentSettlements: number;
  invasions: number;
  conquests: number;
  notableKills: number;
  powerIndex: number;
  factions: DarkForceFaction[];
  timeline: DarkForceEvent[];
  recordedEvents: number;
  firstInvasionYear: number | null;
}

export interface DarkForcesPage {
  items: DarkForce[];
  totalCount: number;
  totalFilteredCount: number;
  fallenCount: number;
  totalInvasions: number;
  maximumTerritory: number;
}

export interface DarkForceFaction {
  id: number;
  name: string;
  fromYear: number;
  throughYear: number | null;
  evidence: string;
}

export interface DarkForceGroup {
  id: number;
  name: string;
  relationship: string;
  holdingId: number | null;
  holdingName: string | null;
}

export interface DarkForceApprentice {
  figureId: number;
  name: string;
  firstLearnedYear: number;
}

export interface DarkForceRaisedFigure {
  figureId: number;
  name: string;
  firstRaisedYear: number;
  recordedRaiseEvents: number;
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
  apprenticeCount: number;
  apprentices: DarkForceApprentice[];
  groups: DarkForceGroup[];
  historicalRaisedCount: number;
  historicalRaiseEvents: number;
  nonhistoricalRaisedCount: number | null;
  unidentifiedRaiseEvents: number;
  raisedHistoricalFigures: DarkForceRaisedFigure[];
}

export interface DarkForcesPage {
  items: DarkForce[];
  totalCount: number;
  totalFilteredCount: number;
  fallenCount: number;
  totalInvasions: number;
  maximumTerritory: number;
}

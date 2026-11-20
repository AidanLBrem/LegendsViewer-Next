export interface IntrigueReference { id: number; name: string; path: string | null }
export interface IntrigueField { name: string; value: string; children: IntrigueField[] }
export interface Conspirator {
  actorId: number; role: string; actorRole: string | null; isTarget: boolean; strategy: string | null;
  figure: IntrigueReference | null; entity: IntrigueReference | null; strategyEntity: IntrigueReference | null;
  strategyPositionId: number; handleActorId: number; agreementId: number; agreementHasMessenger: boolean;
  promisedActorImmortality: boolean; promisedMeImmortality: boolean; fields: IntrigueField[];
}
export interface PlotReference { figureId: number; localId: number; resolved: boolean }
export interface Conspiracy {
  key: string; localId: number; owner: IntrigueReference; objective: string; status: string;
  entity: IntrigueReference | null; artifact: IntrigueReference | null; participants: Conspirator[];
  civilizations: IntrigueReference[]; parent: PlotReference | null; delegated: PlotReference | null;
  fields: IntrigueField[];
}
export interface ConspiracyPage {
  items: Conspiracy[]; totalCount: number; totalFilteredCount: number; civilizations: IntrigueReference[];
}

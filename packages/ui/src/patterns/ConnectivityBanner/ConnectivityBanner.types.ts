export type ConnectivityState = 'online' | 'offlineAllowed' | 'offlineExpired';

export interface ConnectivityBannerProps {
  state: ConnectivityState;
  lastSyncedAt: Date;
  offlineRemainingSeconds?: number;
}

import api from "./api";

export type DashboardCounts = {
  activeTenants: number;
  activeSubscriptions: number;
  expiringSoon: number;
  supportTicketsOpenOrInProgress: number;
  pendingApprovals: number;
  outstandingInvoices: number;
};

export type RecentTenant = {
  tenantId: string;
  tenantName: string;
  status: string;
  expiryDate: string | null;
};

export type DashboardOverview = {
  counts: DashboardCounts;
  recentTenants: RecentTenant[];
};

export async function getDashboardOverview(): Promise<DashboardOverview> {
  const res = await api.get("/api/dashboard/overview");
  return res.data;
}
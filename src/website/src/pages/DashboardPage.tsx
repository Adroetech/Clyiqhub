import React, { useEffect, useState } from "react";
import { Alert, Card, Col, Row, Skeleton, Table, Typography } from "antd";
import { useNavigate } from "react-router-dom";
import { getDashboardOverview, DashboardOverview } from "../services/dashboard";

const { Title } = Typography;

export default function DashboardPage() {
    const nav = useNavigate();
    const [loading, setLoading] = useState(true);
    const [data, setData] = useState<DashboardOverview | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
  let alive = true;

  (async () => {
    setLoading(true);
    setError(null);

    try {
      const timeout = new Promise<never>((_, reject) =>
        setTimeout(() => reject(new Error("Dashboard request timed out")), 8000)
      );

      const result = await Promise.race([getDashboardOverview(), timeout]);
      if (alive) setData(result as DashboardOverview);
    } catch (e: any) {
      const msg =
        e?.response?.data?.message ||
        e?.response?.data?.errorCode ||
        e?.message ||
        "Failed to load dashboard.";
      if (alive) setError(msg);
    } finally {
      if (alive) setLoading(false);
    }
  })();

  return () => {
    alive = false;
  };
}, []);

    // ---- Tile click navigation ----
    // Adjust routes to match your future list pages. For now we’ll create placeholders.
    const go = (path: string) => nav(path);

    const tiles = [
        {
            key: "tenants",
            title: "Tenants",
            value: data?.counts.activeTenants,
            color: "#4b004f",
            onClick: () => go("/tenants?status=Active")
        },
        {
            key: "activeSubs",
            title: "Active Subscriptions",
            value: data?.counts.activeSubscriptions,
            color: "#1b5e20",
            onClick: () => go("/tenant-subscriptions?status=Active")
        },
        {
            key: "expiring",
            title: "Expiring Soon",
            value: data?.counts.expiringSoon,
            color: "#6a1b9a",
            onClick: () => go("/tenant-subscriptions?expiringSoon=1")
        },
        {
            key: "tickets",
            title: "Support Tickets",
            value: data?.counts.supportTicketsOpenOrInProgress,
            color: "#0f4c81",
            onClick: () => go("/support-tickets?status=Open,In-progress")
        },
        {
            key: "pending",
            title: "Pending Approvals",
            value: data?.counts.pendingApprovals,
            color: "#0b5bd3",
            onClick: () => go("/tenants?status=Pending%20Approval")
        },
        {
            key: "invoices",
            title: "Outstanding Invoices",
            value: data?.counts.outstandingInvoices,
            color: "#b45309",
            onClick: () => go("/invoices?status=unpaid,overdue")
        }
    ];

    return (
        <div style={{ padding: 16 }}>
            <Title level={4} style={{ marginBottom: 16 }}>Dashboard Overview</Title>

            {error && (
                <div style={{ marginBottom: 16 }}>
                    <Alert type="error" showIcon message={error} />
                </div>
            )}


            {/* 6 Summary Tiles */}
            <Row gutter={[16, 16]}>
                {tiles.map(t => (
                    <Col key={t.key} xs={24} sm={12} md={8} lg={8} xl={4}>
                        <Card
                            hoverable
                            onClick={t.onClick}
                            style={{
                                background: t.color,
                                color: "#fff",
                                borderRadius: 10,
                                cursor: "pointer",
                                minHeight: 92
                            }}
                            bodyStyle={{ padding: 16 }}
                        >
                            {loading ? (
                                <Skeleton active title={false} paragraph={{ rows: 2 }} />
                            ) : (
                                <>
                                    <div style={{ fontSize: 12, opacity: 0.9 }}>{t.title}</div>
                                    <div style={{ fontSize: 28, fontWeight: 700 }}>
                                        {t.value ?? 0}
                                    </div>
                                </>
                            )}
                        </Card>
                    </Col>
                ))}
            </Row>

            {/* Recent Tenants */}
            <div style={{ marginTop: 24 }}>
                <Title level={5} style={{ marginBottom: 12 }}>Recent Tenants</Title>

                <Table
                    rowKey="tenantId"
                    loading={loading}
                    dataSource={data?.recentTenants ?? []}
                    pagination={false}
                    columns={[
                        { title: "Name", dataIndex: "tenantName" },
                        { title: "Status", dataIndex: "status" },
                        {
                            title: "Expiry Date",
                            dataIndex: "expiryDate",
                            render: (v: string | null) => v ? new Date(v).toLocaleDateString() : "-"
                        }
                    ]}
                />
            </div>
        </div>
    );
}
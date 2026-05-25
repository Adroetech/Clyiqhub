import React from "react";
import { Layout, Menu } from "antd";
import {
  DashboardOutlined,
  TeamOutlined,
  FileTextOutlined,
  CustomerServiceOutlined,
  CreditCardOutlined
} from "@ant-design/icons";
import { useLocation, useNavigate } from "react-router-dom";

const { Sider, Header, Content } = Layout;

export default function Shell({ children }: { children: React.ReactElement }) {
  const nav = useNavigate();
  const loc = useLocation();

  const items = [
    { key: "/dashboard", icon: <DashboardOutlined />, label: "Dashboard" },
    { key: "/tenants", icon: <TeamOutlined />, label: "Tenants" },
    { key: "/tenant-subscriptions", icon: <FileTextOutlined />, label: "Subscriptions" },
    { key: "/support-tickets", icon: <CustomerServiceOutlined />, label: "Support Tickets" },
    { key: "/invoices", icon: <CreditCardOutlined />, label: "Invoices" }
  ];

  // highlight based on current path prefix
  const selected =
    items.find(i => loc.pathname.startsWith(i.key))?.key ?? "/dashboard";

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Sider width={220} theme="light">
        <div style={{ padding: 16, fontWeight: 700, color: "#1677ff" }}>
          ClyiqHub
        </div>
        <Menu
          mode="inline"
          selectedKeys={[selected]}
          items={items}
          onClick={(e) => nav(e.key)}
        />
      </Sider>

      <Layout>
        <Header style={{ background: "#fff", borderBottom: "1px solid #eee" }}>
          <div style={{ fontWeight: 600 }}>Overview</div>
        </Header>

        <Content style={{ background: "#f6f8fb" }}>
          {children}
        </Content>
      </Layout>
    </Layout>
  );
}
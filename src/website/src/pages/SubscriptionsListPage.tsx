import { useLocation } from "react-router-dom";

export default function SubscriptionsListPage() {
  const loc = useLocation();
  return (
    <div style={{ padding: 16 }}>
      <h3>Tenant Subscriptions</h3>
      <p>Filter: {loc.search}</p>
    </div>
  );
}
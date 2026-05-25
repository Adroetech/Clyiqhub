import { useLocation } from "react-router-dom";

export default function TenantsListPage() {
  const loc = useLocation();
  return (
    <div style={{ padding: 16 }}>
      <h3>Tenants</h3>
      <p>Filter: {loc.search}</p>
    </div>
  );
}
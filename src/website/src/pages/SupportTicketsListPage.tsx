import { useLocation } from "react-router-dom";

export default function SupportTicketsListPage() {
  const loc = useLocation();
  return (
    <div style={{ padding: 16 }}>
      <h3>Support Tickets</h3>
      <p>Filter: {loc.search}</p>
    </div>
  );
}

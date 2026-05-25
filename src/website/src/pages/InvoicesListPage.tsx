import { useLocation } from "react-router-dom";

export default function InvoicesListPage() {
  const loc = useLocation();
  return (
    <div style={{ padding: 16 }}>
      <h3>Invoices</h3>
      <p>Filter: {loc.search}</p>
    </div>
  );
}
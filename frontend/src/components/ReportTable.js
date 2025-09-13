import React from 'react';
function ReportTable({ data }) {
  const exportCSV = () => {
    const rows = ["Resource,kWh,CarbonKg", ...data.map(d => `${d.resource},${d.kWh},${d.carbonKg}`)];
    const blob = new Blob([rows.join('\n')], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'energy_report.csv';
    a.click();
  };
  return (
    <div>
      <button className="btn btn-secondary mb-2" onClick={exportCSV}>Export CSV</button>
      <table className="table table-bordered">
        <thead><tr><th>Resource</th><th>kWh</th><th>CarbonKg</th></tr></thead>
        <tbody>
          {data.map((d,i) => <tr key={i}><td>{d.resource}</td><td>{d.kWh}</td><td>{d.carbonKg}</td></tr>)}
        </tbody>
      </table>
    </div>
  );
}
export default ReportTable;

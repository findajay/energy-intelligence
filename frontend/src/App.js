import React, { useState } from 'react';
import Dashboard from './components/Dashboard';
import ResourceDiscovery from './components/ResourceDiscovery';
import 'bootstrap/dist/css/bootstrap.min.css';

function App() {
  const [activeView, setActiveView] = useState('dashboard');

  return (
    <div className="App">
      <nav className="navbar navbar-expand-lg" style={{ background: 'var(--eco-primary)' }}>
        <div className="container">
          <a className="navbar-brand text-white d-flex align-items-center" href="#!" style={{ fontSize: '1.3rem', fontWeight: 'bold' }}>
            🌱 Energy Intelligence
          </a>
          <div className="navbar-nav">
            <button 
              className={`nav-link btn btn-link text-white ${activeView === 'dashboard' ? 'active' : ''}`}
              onClick={() => setActiveView('dashboard')}
            >
              Dashboard
            </button>
            <button 
              className={`nav-link btn btn-link text-white ${activeView === 'discovery' ? 'active' : ''}`}
              onClick={() => setActiveView('discovery')}
            >
              Resource Discovery
            </button>
          </div>
        </div>
      </nav>
      
      {activeView === 'dashboard' && <Dashboard />}
      {activeView === 'discovery' && <ResourceDiscovery />}
    </div>
  );
}

export default App;

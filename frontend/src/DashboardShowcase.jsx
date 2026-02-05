import { useEffect } from "react";

const layoutIds = [1, 2, 3, 4, 5];

const LayoutSwitcher = ({ active, label = "Layouts" }) => (
  <div className={`dash-switcher dash-switcher--${active}`}>
    <span className="dash-switcher__label">{label}</span>
    <div className="dash-switcher__links">
      {layoutIds.map((id) => (
        <a
          key={id}
          className={id === active ? "is-active" : ""}
          href={`/${id}`}
          aria-label={`Open layout ${id}`}
        >
          {id}
        </a>
      ))}
    </div>
  </div>
);

const useDashboardBody = (variant) => {
  useEffect(() => {
    const baseClass = "dashboard-body";
    const variantClass = `dashboard-body--${variant}`;
    document.body.classList.add(baseClass, variantClass);
    return () => {
      document.body.classList.remove(baseClass, variantClass);
    };
  }, [variant]);
};

const DashboardOne = () => (
  <section className="dash-root dash-root--1">
    <header className="dash1-top">
      <div className="dash1-brand">
        <div className="dash1-logo">Aegis</div>
        <div className="dash1-sub">Security Command</div>
      </div>
      <nav className="dash1-top-nav">
        <a className="is-active" href="/1">Overview</a>
        <a href="/1">Policies</a>
        <a href="/1">Signals</a>
        <a href="/1">Assets</a>
        <a href="/1">Reports</a>
      </nav>
      <div className="dash1-actions">
        <button className="dash-btn dash-btn--ghost" type="button">
          Create report
        </button>
        <button className="dash-btn" type="button">
          Deploy policy
        </button>
      </div>
    </header>
    <div className="dash1-body">
      <aside className="dash1-rail">
        <div className="dash1-rail-card">
          <span className="dash-label">Tenant</span>
          <strong>Northstar Holdings</strong>
          <p className="dash-muted">Last audit 6 days ago</p>
        </div>
        <LayoutSwitcher active={1} label="Layouts" />
        <div className="dash1-rail-list">
          <span className="dash-label">Quick actions</span>
          <button className="dash1-rail-button" type="button">
            Run exposure scan
          </button>
          <button className="dash1-rail-button" type="button">
            Schedule advisory
          </button>
          <button className="dash1-rail-button" type="button">
            Sync asset inventory
          </button>
        </div>
        <div className="dash1-rail-card">
          <span className="dash-label">On-call</span>
          <strong>Priya Narang</strong>
          <p className="dash-muted">Primary escalation</p>
          <button className="dash-btn dash-btn--ghost" type="button">
            Notify team
          </button>
        </div>
      </aside>
      <main className="dash1-main">
        <div className="dash-card dash1-hero">
          <div className="dash1-hero-copy">
            <span className="dash-label">Risk posture</span>
            <h1>92% secure</h1>
            <p>
              Exposure trending down with policy automation. 3 high-priority findings remain
              across identity and storage controls.
            </p>
            <div className="dash1-hero-actions">
              <button className="dash-btn" type="button">
                View findings
              </button>
              <button className="dash-btn dash-btn--ghost" type="button">
                Export executive brief
              </button>
            </div>
          </div>
          <div className="dash1-hero-chart">
            <span className="dash-label">30-day trend</span>
            <svg viewBox="0 0 260 120" role="img" aria-label="Risk trend chart">
              <path
                d="M10 86 L45 76 L70 74 L95 68 L120 60 L145 52 L170 48 L195 40 L220 34 L250 28"
                fill="none"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                d="M10 110 L45 98 L70 94 L95 88 L120 78 L145 70 L170 62 L195 54 L220 50 L250 44 L250 110 Z"
                fill="currentColor"
                opacity="0.15"
              />
            </svg>
            <div className="dash1-hero-foot">
              <span className="dash-pill">+8% QoQ</span>
              <span className="dash-muted">Automated fixes 142</span>
            </div>
          </div>
        </div>
        <div className="dash1-kpis">
          {[
            { label: "Critical findings", value: "3", detail: "-2 this week" },
            { label: "Assets monitored", value: "1,284", detail: "99.1% coverage" },
            { label: "Mean time to resolve", value: "5.4d", detail: "Enterprise SLA" },
            { label: "Policy drift", value: "0.8%", detail: "Below threshold" }
          ].map((item) => (
            <div key={item.label} className="dash-card dash1-kpi">
              <span className="dash-label">{item.label}</span>
              <strong>{item.value}</strong>
              <span className="dash-muted">{item.detail}</span>
            </div>
          ))}
        </div>
        <div className="dash1-panels">
          <div className="dash-card dash1-panel">
            <div className="dash-panel__header">
              <h3>Active programs</h3>
              <button className="dash-btn dash-btn--ghost" type="button">
                View all
              </button>
            </div>
            <div className="dash-table">
              <div className="dash-table__row dash-table__head">
                <span>Program</span>
                <span>Owner</span>
                <span>Status</span>
              </div>
              {[
                ["Identity hardening", "S. Walters", "On track"],
                ["Cloud perimeter", "M. Zhao", "At risk"],
                ["Endpoint uplift", "E. Vance", "On track"],
                ["Vendor assurance", "R. Silva", "Review"],
                ["Data residency", "T. Rocha", "On track"]
              ].map((row) => (
                <div key={row[0]} className="dash-table__row">
                  <span>{row[0]}</span>
                  <span className="dash-muted">{row[1]}</span>
                  <span className="dash-chip">{row[2]}</span>
                </div>
              ))}
            </div>
          </div>
          <div className="dash-card dash1-panel">
            <div className="dash-panel__header">
              <h3>Signal overview</h3>
              <span className="dash-pill">Live</span>
            </div>
            <div className="dash1-signal-grid">
              {[
                { label: "Identity", value: "42%", tone: "strong" },
                { label: "Network", value: "28%", tone: "neutral" },
                { label: "Storage", value: "18%", tone: "warn" },
                { label: "Apps", value: "12%", tone: "neutral" }
              ].map((item) => (
                <div key={item.label} className={`dash1-signal dash1-signal--${item.tone}`}>
                  <span className="dash-label">{item.label}</span>
                  <strong>{item.value}</strong>
                </div>
              ))}
            </div>
            <div className="dash1-signal-bar">
              <div className="dash1-signal-bar__fill" />
              <span className="dash-muted">Signal confidence 96%</span>
            </div>
          </div>
        </div>
      </main>
    </div>
  </section>
);

const DashboardTwo = () => (
  <section className="dash-root dash-root--2">
    <header className="dash2-top">
      <div className="dash2-brand">
        <span className="dash2-logo">Lattice</span>
        <span className="dash2-sub">Finance Intelligence</span>
      </div>
      <div className="dash2-search">
        <input placeholder="Search entities, cost centers, accounts" aria-label="Search" />
        <span className="dash2-search__hint">/ to focus</span>
      </div>
      <div className="dash2-actions">
        <button className="dash-btn dash-btn--ghost" type="button">
          Share workspace
        </button>
        <button className="dash-btn" type="button">
          New scenario
        </button>
      </div>
    </header>
    <div className="dash2-grid">
      <aside className="dash2-left">
        <LayoutSwitcher active={2} label="Layouts" />
        <div className="dash2-card">
          <span className="dash-label">Cash position</span>
          <h2>$184.2M</h2>
          <p className="dash-muted">Runway 22 months at current burn.</p>
          <div className="dash2-mini">
            <span>Liquidity ratio</span>
            <strong>1.8x</strong>
          </div>
        </div>
        <div className="dash2-card">
          <span className="dash-label">Forecast accuracy</span>
          <h2>97.4%</h2>
          <p className="dash-muted">Variance down 3% versus last quarter.</p>
        </div>
        <div className="dash2-card">
          <span className="dash-label">Savings pipeline</span>
          <div className="dash2-tags">
            <span className="dash-tag">Contracts</span>
            <span className="dash-tag">Cloud</span>
            <span className="dash-tag">HR</span>
            <span className="dash-tag">Facilities</span>
          </div>
          <p className="dash-muted">$6.4M identified, $2.2M approved.</p>
        </div>
      </aside>
      <main className="dash2-center">
        <div className="dash2-card dash2-hero">
          <div className="dash2-hero__head">
            <div>
              <span className="dash-label">Cost trajectory</span>
              <h1>Enterprise spend</h1>
              <p className="dash-muted">
                Consolidated view across 18 business units with predictive trendlines.
              </p>
            </div>
            <div className="dash2-hero__filters">
              <button className="dash-btn dash-btn--ghost" type="button">
                FY 2026
              </button>
              <button className="dash-btn dash-btn--ghost" type="button">
                Global
              </button>
            </div>
          </div>
          <div className="dash2-chart">
            <svg viewBox="0 0 320 140" role="img" aria-label="Spend chart">
              <path
                d="M10 110 C60 90 80 60 120 68 C160 76 200 40 240 44 C270 48 290 30 310 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="4"
              />
              <path
                d="M10 110 C60 90 80 60 120 68 C160 76 200 40 240 44 C270 48 290 30 310 24 L310 130 L10 130 Z"
                fill="currentColor"
                opacity="0.12"
              />
            </svg>
            <div className="dash2-chart__legend">
              <span className="dash-tag">Actuals</span>
              <span className="dash-tag">Forecast</span>
              <span className="dash-tag">Savings</span>
            </div>
          </div>
        </div>
        <div className="dash2-card">
          <div className="dash-panel__header">
            <h3>Top variances</h3>
            <button className="dash-btn dash-btn--ghost" type="button">
              Export
            </button>
          </div>
          <div className="dash2-table">
            {[
              ["Cloud infrastructure", "+$1.2M", "Capacity expansion"],
              ["Sales commissions", "-$420k", "Headcount delta"],
              ["Data center", "+$312k", "Energy surcharge"],
              ["Professional services", "-$210k", "Contract consolidation"],
              ["Corporate travel", "+$98k", "APAC sessions"]
            ].map((row) => (
              <div key={row[0]} className="dash2-table__row">
                <span>{row[0]}</span>
                <strong>{row[1]}</strong>
                <span className="dash-muted">{row[2]}</span>
              </div>
            ))}
          </div>
        </div>
      </main>
      <aside className="dash2-right">
        <div className="dash2-card">
          <span className="dash-label">Approval queue</span>
          <ul className="dash2-queue">
            {[
              ["Renew vendor A", "$420k", "CFO"],
              ["APAC headcount", "$1.1M", "HR"],
              ["Security uplift", "$980k", "CISO"],
              ["Facilities refresh", "$260k", "COO"]
            ].map((item) => (
              <li key={item[0]}>
                <div>
                  <strong>{item[0]}</strong>
                  <span className="dash-muted">{item[2]}</span>
                </div>
                <span className="dash-chip">{item[1]}</span>
              </li>
            ))}
          </ul>
        </div>
        <div className="dash2-card dash2-notes">
          <span className="dash-label">Leadership notes</span>
          <p>
            Maintain margin guardrails in Q2. Prioritize savings initiatives tied to cloud
            commitments.
          </p>
          <button className="dash-btn" type="button">
            Draft update
          </button>
        </div>
      </aside>
    </div>
  </section>
);

const DashboardThree = () => (
  <section className="dash-root dash-root--3">
    <header className="dash3-top">
      <div className="dash3-brand">
        <span className="dash3-logo">Signal Fabric</span>
        <span className="dash3-sub">Operations Mesh</span>
      </div>
      <nav className="dash3-top-nav">
        <a className="is-active" href="/3">Pulse</a>
        <a href="/3">Incidents</a>
        <a href="/3">Capacity</a>
        <a href="/3">Automation</a>
      </nav>
      <LayoutSwitcher active={3} label="Layouts" />
    </header>
    <div className="dash3-shell">
      <aside className="dash3-metrics">
        <div className="dash3-score">
          <span className="dash-label">Ops score</span>
          <strong>88</strong>
          <span className="dash-muted">+6 this week</span>
        </div>
        <div className="dash3-metric">
          <span className="dash-label">Latency</span>
          <strong>126ms</strong>
          <div className="dash3-bar">
            <span style={{ width: "72%" }} />
          </div>
        </div>
        <div className="dash3-metric">
          <span className="dash-label">Queue depth</span>
          <strong>0.7x</strong>
          <div className="dash3-bar">
            <span style={{ width: "38%" }} />
          </div>
        </div>
        <div className="dash3-metric">
          <span className="dash-label">Automation coverage</span>
          <strong>64%</strong>
          <div className="dash3-bar">
            <span style={{ width: "64%" }} />
          </div>
        </div>
        <div className="dash3-metric">
          <span className="dash-label">Service tier</span>
          <strong>Platinum</strong>
          <span className="dash-chip">Signed</span>
        </div>
      </aside>
      <main className="dash3-stream">
        <div className="dash3-panel">
          <div className="dash-panel__header">
            <h2>Incident stream</h2>
            <button className="dash-btn dash-btn--ghost" type="button">
              Filter
            </button>
          </div>
          <div className="dash3-feed">
            {[
              ["APAC edge", "Packet loss stabilized", "4 min"],
              ["Payments API", "Latency regression mitigated", "11 min"],
              ["Warehouse sync", "Backfill running", "18 min"],
              ["Auth gateway", "Credential drift flagged", "32 min"],
              ["B2B portal", "Cache invalidation rolling", "47 min"]
            ].map((row) => (
              <div key={row[0]} className="dash3-feed__item">
                <div>
                  <strong>{row[0]}</strong>
                  <span className="dash-muted">{row[1]}</span>
                </div>
                <span className="dash-chip">{row[2]}</span>
              </div>
            ))}
          </div>
        </div>
        <div className="dash3-panel dash3-grid">
          <div className="dash3-chart">
            <span className="dash-label">Capacity planner</span>
            <svg viewBox="0 0 240 140" role="img" aria-label="Capacity chart">
              <rect x="18" y="20" width="36" height="100" />
              <rect x="72" y="40" width="36" height="80" />
              <rect x="126" y="30" width="36" height="90" />
              <rect x="180" y="10" width="36" height="110" />
            </svg>
            <span className="dash-muted">Projected peak 74% utilization</span>
          </div>
          <div className="dash3-card">
            <span className="dash-label">Runbook status</span>
            <h3>18 playbooks active</h3>
            <p className="dash-muted">Two updated in the last 24 hours.</p>
          </div>
          <div className="dash3-card">
            <span className="dash-label">Change window</span>
            <h3>Fri 02:00 - 05:00 UTC</h3>
            <p className="dash-muted">7 changes approved, 2 pending.</p>
          </div>
        </div>
      </main>
      <aside className="dash3-detail">
        <div className="dash3-detail__card">
          <span className="dash-label">Priority focus</span>
          <h3>Stabilize payment edge</h3>
          <p className="dash-muted">
            Consolidate routing rules and verify auto-failover across 3 regions.
          </p>
          <div className="dash3-detail__actions">
            <button className="dash-btn" type="button">
              Open runbook
            </button>
            <button className="dash-btn dash-btn--ghost" type="button">
              Assign lead
            </button>
          </div>
        </div>
        <div className="dash3-detail__card">
          <span className="dash-label">Escalations</span>
          <ul className="dash3-list">
            <li>
              <strong>Billing sync</strong>
              <span className="dash-muted">Awaiting vendor response</span>
            </li>
            <li>
              <strong>Inventory drift</strong>
              <span className="dash-muted">Patch scheduled</span>
            </li>
            <li>
              <strong>Telemetry gaps</strong>
              <span className="dash-muted">Two regions impacted</span>
            </li>
          </ul>
        </div>
      </aside>
    </div>
  </section>
);

const DashboardFour = () => (
  <section className="dash-root dash-root--4">
    <header className="dash4-top">
      <div className="dash4-brand">
        <span className="dash4-logo">Harbor</span>
        <span className="dash4-sub">Cloud Control</span>
      </div>
      <div className="dash4-top-actions">
        <button className="dash-btn dash-btn--ghost" type="button">
          Status
        </button>
        <button className="dash-btn" type="button">
          Launch workflow
        </button>
      </div>
    </header>
    <div className="dash4-body">
      <aside className="dash4-nav">
        <LayoutSwitcher active={4} label="Layouts" />
        <nav className="dash4-nav__list">
          <a className="is-active" href="/4">Overview</a>
          <a href="/4">Workflows</a>
          <a href="/4">Integrations</a>
          <a href="/4">SLOs</a>
          <a href="/4">FinOps</a>
        </nav>
        <div className="dash4-nav__card">
          <span className="dash-label">Environment</span>
          <strong>Production</strong>
          <p className="dash-muted">6 regions online</p>
        </div>
      </aside>
      <main className="dash4-main">
        <div className="dash4-hero">
          <div>
            <span className="dash-label">Workflow health</span>
            <h1>99.97% uptime</h1>
            <p className="dash-muted">
              All critical workloads running within SLA. Next maintenance window in 14 days.
            </p>
          </div>
          <div className="dash4-hero__stats">
            <div>
              <strong>128</strong>
              <span className="dash-muted">Pipelines</span>
            </div>
            <div>
              <strong>24</strong>
              <span className="dash-muted">Automations</span>
            </div>
            <div>
              <strong>6</strong>
              <span className="dash-muted">Regions</span>
            </div>
          </div>
        </div>
        <div className="dash4-lanes">
          {[
            {
              title: "Design",
              items: [
                "Capacity plan update",
                "Latency budget review",
                "Runbook refresh"
              ]
            },
            {
              title: "Build",
              items: [
                "Edge mesh rollout",
                "Token rotation",
                "Data lake policy"
              ]
            },
            {
              title: "Operate",
              items: [
                "Stability checklist",
                "Post-incident follow-up",
                "Quarterly failover test"
              ]
            }
          ].map((lane) => (
            <div key={lane.title} className="dash4-lane">
              <div className="dash4-lane__header">
                <h3>{lane.title}</h3>
                <span className="dash-pill">{lane.items.length} items</span>
              </div>
              {lane.items.map((item) => (
                <div key={item} className="dash4-card">
                  <strong>{item}</strong>
                  <span className="dash-muted">Owner: Platform team</span>
                  <div className="dash4-card__meta">
                    <span className="dash-chip">In progress</span>
                    <span className="dash-muted">ETA 3d</span>
                  </div>
                </div>
              ))}
            </div>
          ))}
        </div>
      </main>
    </div>
  </section>
);

const DashboardFive = () => (
  <section className="dash-root dash-root--5">
    <header className="dash5-top">
      <div className="dash5-brand">
        <span className="dash5-logo">Atlas</span>
        <span className="dash5-sub">Compliance Hub</span>
      </div>
      <nav className="dash5-nav">
        <a className="is-active" href="/5">Readiness</a>
        <a href="/5">Controls</a>
        <a href="/5">Evidence</a>
        <a href="/5">Audits</a>
      </nav>
      <LayoutSwitcher active={5} label="Layouts" />
    </header>
    <div className="dash5-body">
      <aside className="dash5-left">
        <div className="dash5-card">
          <span className="dash-label">Readiness score</span>
          <h1>84%</h1>
          <p className="dash-muted">Up 9% since last quarter. 12 controls pending review.</p>
        </div>
        <div className="dash5-card">
          <span className="dash-label">Control coverage</span>
          <div className="dash5-meter">
            <span style={{ width: "84%" }} />
          </div>
          <div className="dash5-tags">
            <span className="dash-chip">SOC 2</span>
            <span className="dash-chip">ISO 27001</span>
            <span className="dash-chip">HIPAA</span>
          </div>
        </div>
      </aside>
      <main className="dash5-timeline">
        <div className="dash5-card">
          <div className="dash-panel__header">
            <h2>Audit timeline</h2>
            <button className="dash-btn dash-btn--ghost" type="button">
              View calendar
            </button>
          </div>
          <div className="dash5-steps">
            {[
              ["Evidence collection", "Feb 10", "In progress"],
              ["Control testing", "Feb 18", "Scheduled"],
              ["Executive review", "Mar 1", "Upcoming"],
              ["External audit", "Mar 12", "Locked"]
            ].map((step) => (
              <div key={step[0]} className="dash5-step">
                <div className="dash5-step__dot" />
                <div>
                  <strong>{step[0]}</strong>
                  <span className="dash-muted">{step[1]}</span>
                </div>
                <span className="dash-chip">{step[2]}</span>
              </div>
            ))}
          </div>
        </div>
        <div className="dash5-card">
          <div className="dash-panel__header">
            <h3>Evidence requests</h3>
            <button className="dash-btn dash-btn--ghost" type="button">
              Assign
            </button>
          </div>
          <div className="dash5-evidence">
            {[
              ["Access review", "IT Security", "Due Feb 12"],
              ["Vendor risk", "Procurement", "Due Feb 14"],
              ["Encryption attestation", "Infrastructure", "Due Feb 15"],
              ["Incident response", "Operations", "Due Feb 17"]
            ].map((item) => (
              <div key={item[0]} className="dash5-evidence__item">
                <div>
                  <strong>{item[0]}</strong>
                  <span className="dash-muted">{item[1]}</span>
                </div>
                <span className="dash-pill">{item[2]}</span>
              </div>
            ))}
          </div>
        </div>
      </main>
      <aside className="dash5-right">
        <div className="dash5-card">
          <span className="dash-label">Priority tasks</span>
          <ul className="dash5-list">
            {[
              "Review privileged access logs",
              "Complete Q1 risk narrative",
              "Update vendor criticality",
              "Confirm DR test evidence"
            ].map((item) => (
              <li key={item}>
                <span className="dash5-list__check" />
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </div>
        <div className="dash5-card">
          <span className="dash-label">Auditor notes</span>
          <p>
            Focus on evidence continuity and ensure ownership is clear for each control. Provide
            updated risk assessment by Feb 9.
          </p>
          <button className="dash-btn" type="button">
            Share update
          </button>
        </div>
      </aside>
    </div>
  </section>
);

export default function DashboardShowcase({ variant }) {
  useDashboardBody(variant);

  switch (variant) {
    case 1:
      return <DashboardOne />;
    case 2:
      return <DashboardTwo />;
    case 3:
      return <DashboardThree />;
    case 4:
      return <DashboardFour />;
    case 5:
      return <DashboardFive />;
    default:
      return (
        <section className="dash-root">
          <p>Unknown layout.</p>
        </section>
      );
  }
}

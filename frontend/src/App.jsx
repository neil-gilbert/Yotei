import { useEffect, useMemo, useState } from "react";
import ReactFlow, { Background, Controls } from "reactflow";
import "reactflow/dist/style.css";
import LogoStandard from "./Images/Logo-Standard.png";

const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:8080";

const flowColumns = ["entry", "file", "side_effect", "other"];
const flowColumnX = {
  entry: 0,
  file: 320,
  side_effect: 640,
  other: 920
};
const dashboardLayouts = [
  {
    id: "finbank",
    name: "FinBank",
    description: "Dark finance dashboard with compact KPI tiles."
  },
  {
    id: "wehr-classic",
    name: "WeHR Classic",
    description: "Clean HR dashboard with soft metric cards."
  },
  {
    id: "wehr-split",
    name: "WeHR Split",
    description: "Dual-column dashboard with activity emphasis."
  },
  {
    id: "health-split",
    name: "Health Split",
    description: "Clinical split-pane dashboard with dark right rail."
  },
  {
    id: "ops-night",
    name: "Ops Night",
    description: "Mission-control style admin cockpit."
  }
];

const dashboardPrimaryNav = [
  { key: "dashboard", label: "Dashboard", short: "DB" },
  { key: "recruitment", label: "Recruitment", short: "RC" },
  { key: "schedule", label: "Schedule", short: "SC" },
  { key: "employees", label: "Employees", short: "EM" },
  { key: "departments", label: "Departments", short: "DP" }
];

const dashboardSecondaryNav = [
  { key: "support", label: "Support", short: "SP" },
  { key: "settings", label: "Settings", short: "ST" }
];

export default function App() {
  const [tenantToken, setTenantToken] = useState(
    () => localStorage.getItem("yoteiTenantToken") ?? ""
  );
  const [activeView, setActiveView] = useState(() =>
    localStorage.getItem("yoteiTenantToken") ? "dashboard" : "setup"
  );
  const [selectedId, setSelectedId] = useState(null);
  const [pendingPrLookup, setPendingPrLookup] = useState(null);
  const [autoResolveLatest, setAutoResolveLatest] = useState(false);
  const [detail, setDetail] = useState(null);
  const [fileChanges, setFileChanges] = useState([]);
  const [selectedChange, setSelectedChange] = useState(null);
  const [changeTree, setChangeTree] = useState([]);
  const [treeError, setTreeError] = useState("");
  const [buildStatus, setBuildStatus] = useState("idle");
  const [buildError, setBuildError] = useState("");
  const [summary, setSummary] = useState(null);
  const [summaryStatus, setSummaryStatus] = useState("idle");
  const [summaryError, setSummaryError] = useState("");
  const [selectedNode, setSelectedNode] = useState(null);
  const [behaviourSummary, setBehaviourSummary] = useState(null);
  const [behaviourStatus, setBehaviourStatus] = useState("idle");
  const [behaviourError, setBehaviourError] = useState("");
  const [checklist, setChecklist] = useState([]);
  const [checklistStatus, setChecklistStatus] = useState("idle");
  const [checklistError, setChecklistError] = useState("");
  const [checklistDraft, setChecklistDraft] = useState("");
  const [checklistAddStatus, setChecklistAddStatus] = useState("idle");
  const [checklistAddError, setChecklistAddError] = useState("");
  const [reviewerQuestions, setReviewerQuestions] = useState([]);
  const [questionsStatus, setQuestionsStatus] = useState("idle");
  const [questionsError, setQuestionsError] = useState("");
  const [questionsSource, setQuestionsSource] = useState("");
  const [centerTab, setCenterTab] = useState("summary");
  const [rightTab, setRightTab] = useState("review");
  const [diffText, setDiffText] = useState("");
  const [diffStatus, setDiffStatus] = useState("idle");
  const [flowGraph, setFlowGraph] = useState({ nodes: [], edges: [] });
  const [flowStatus, setFlowStatus] = useState("idle");
  const [flowError, setFlowError] = useState("");
  const [selectedFlowNodeId, setSelectedFlowNodeId] = useState(null);
  const [chatDraft, setChatDraft] = useState("");
  const [chatStatus, setChatStatus] = useState("idle");
  const [chatError, setChatError] = useState("");
  const [chatOpen, setChatOpen] = useState(false);
  const [chatExpanded, setChatExpanded] = useState(false);
  const [chatSoundMuted, setChatSoundMuted] = useState(false);
  const [transcriptEntries, setTranscriptEntries] = useState([]);
  const [transcriptStatus, setTranscriptStatus] = useState("idle");
  const [transcriptError, setTranscriptError] = useState("");
  const [transcriptExportStatus, setTranscriptExportStatus] = useState("idle");
  const [fullDiffStatus, setFullDiffStatus] = useState("idle");
  const [fullDiffError, setFullDiffError] = useState("");
  const [fullDiffActivePath, setFullDiffActivePath] = useState(null);
  const [fullDiffMode, setFullDiffMode] = useState("single");
  const [fullDiffFilter, setFullDiffFilter] = useState("");
  const [fullDiffCache, setFullDiffCache] = useState({});
  const [complianceReport, setComplianceReport] = useState(null);
  const [complianceStatus, setComplianceStatus] = useState("idle");
  const [complianceError, setComplianceError] = useState("");
  const [orgInsights, setOrgInsights] = useState(null);
  const [insightsStatus, setInsightsStatus] = useState("idle");
  const [insightsError, setInsightsError] = useState("");
  const [insightsScope, setInsightsScope] = useState("org");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [changeTypeFilter, setChangeTypeFilter] = useState("");
  const [pathPrefixFilter, setPathPrefixFilter] = useState("");
  const [copiedLabel, setCopiedLabel] = useState("");
  const [dashboardLayout, setDashboardLayout] = useState(() => {
    const savedLayout = localStorage.getItem("yoteiDashboardLayout");
    return dashboardLayouts.some((layout) => layout.id === savedLayout)
      ? savedLayout
      : dashboardLayouts[0].id;
  });
  const normalizedApiBase = useMemo(() => apiBase.replace(/\/+$/, ""), [apiBase]);
  const setupMode = import.meta.env.VITE_SETUP_MODE ?? "customer";
  const installUrl = import.meta.env.VITE_GITHUB_APP_INSTALL_URL ?? "";
  const isAdminSetup = setupMode.toLowerCase() === "admin";
  const webhookUrl = `${normalizedApiBase}/ingest/github/webhook`;
  const ingestUrl = `${normalizedApiBase}/ingest/github`;
  const syncUrl = `${normalizedApiBase}/ingest/github/sync`;

  const apiFetch = async (path, options = {}) => {
    const resolvedPath = path.startsWith("http")
      ? path
      : `${normalizedApiBase}${path.startsWith("/") ? "" : "/"}${path}`;
    const headers = new Headers(options.headers ?? {});
    if (tenantToken) {
      headers.set("X-Tenant-Token", tenantToken);
    }
    return fetch(resolvedPath, { ...options, headers });
  };

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const tokenParam = params.get("tenant") ?? params.get("tenantToken");
    const viewParam = params.get("view");
    const sessionParam =
      params.get("session") ?? params.get("snapshot") ?? params.get("sessionId");
    const ownerParam = params.get("owner") ?? params.get("repoOwner");
    const nameParam = params.get("name") ?? params.get("repo");
    const prParam = params.get("pr") ?? params.get("prNumber");
    const parsedPrNumber = prParam ? Number(prParam) : null;
    const hasValidPrLookup =
      ownerParam &&
      nameParam &&
      Number.isFinite(parsedPrNumber) &&
      parsedPrNumber > 0;
    let updated = false;

    if (tokenParam) {
      localStorage.setItem("yoteiTenantToken", tokenParam);
      setTenantToken(tokenParam);
      updated = true;
    }

    if (viewParam) {
      setActiveView(viewParam);
      updated = true;
    }

    if (hasValidPrLookup) {
      setPendingPrLookup({
        owner: ownerParam,
        name: nameParam,
        prNumber: parsedPrNumber
      });
      updated = true;
    } else if (sessionParam) {
      setSelectedId(sessionParam);
      setAutoResolveLatest(true);
      updated = true;
    }

    if (updated) {
      const nextParams = new URLSearchParams();
      if (hasValidPrLookup) {
        nextParams.set("owner", ownerParam);
        nextParams.set("name", nameParam);
        nextParams.set("prNumber", String(parsedPrNumber));
      } else if (sessionParam) {
        nextParams.set("session", sessionParam);
      }
      const nextUrl = nextParams.toString()
        ? `${window.location.pathname}?${nextParams.toString()}`
        : window.location.pathname;
      window.history.replaceState(null, "", nextUrl);
    }
  }, []);

  useEffect(() => {
    if (!tenantToken || !pendingPrLookup) {
      return;
    }
    resolveLatestSession(pendingPrLookup);
  }, [tenantToken, pendingPrLookup]);

  useEffect(() => {
    const handleStorage = (event) => {
      if (event.key !== "yoteiTenantToken") {
        return;
      }
      setTenantToken(event.newValue ?? "");
    };

    window.addEventListener("storage", handleStorage);
    return () => window.removeEventListener("storage", handleStorage);
  }, []);

  useEffect(() => {
    localStorage.setItem("yoteiDashboardLayout", dashboardLayout);
  }, [dashboardLayout]);

  useEffect(() => {
    document.documentElement.setAttribute("data-layout", dashboardLayout);
  }, [dashboardLayout]);

  useEffect(() => {
    if (!tenantToken && activeView !== "setup") {
      setActiveView("setup");
      return;
    }

    if (tenantToken && activeView === "setup") {
      setActiveView("dashboard");
    }
  }, [tenantToken, activeView]);

  const handleCopy = async (value, label) => {
    if (!value) {
      return;
    }
    try {
      await navigator.clipboard.writeText(value);
      setCopiedLabel(label);
      setTimeout(() => setCopiedLabel(""), 2000);
    } catch (err) {
      setCopiedLabel("failed");
    }
  };

  const handleTenantReset = () => {
    localStorage.removeItem("yoteiTenantToken");
    setTenantToken("");
    setActiveView("setup");
  };

  const refreshSession = () => {
    if (!selectedId) {
      return;
    }
    fetchDetail(selectedId);
    fetchFileChanges(selectedId);
    fetchChangeTree(selectedId);
    fetchSummary(selectedId);
    fetchFlowGraph(selectedId);
    fetchTranscript(selectedId);
  };

  const fetchDetail = async (sessionId) => {
    if (!sessionId) {
      setDetail(null);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${sessionId}`);
      if (!res.ok) {
        throw new Error("Failed to load review session detail");
      }
      const data = await res.json();
      setDetail(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const resolveLatestSession = async (lookup) => {
    if (!lookup?.owner || !lookup?.name || !lookup?.prNumber) {
      return;
    }
    setLoading(true);
    setError("");
    try {
      const params = new URLSearchParams({
        owner: lookup.owner,
        name: lookup.name,
        prNumber: String(lookup.prNumber)
      });
      const res = await apiFetch(`${apiBase}/review-sessions/latest?${params.toString()}`);
      if (res.status === 404) {
        if (!selectedId) {
          setError("No review session found for this pull request yet.");
        }
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to resolve latest review session");
      }
      const data = await res.json();
      if (data?.id) {
        setSelectedId(data.id);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const fetchFileChanges = async (snapshotId) => {
    if (!snapshotId) {
      setFileChanges([]);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const params = new URLSearchParams();
      if (changeTypeFilter) {
        params.set("changeType", changeTypeFilter);
      }
      if (pathPrefixFilter) {
        params.set("pathPrefix", pathPrefixFilter);
      }
      const query = params.toString();
      const res = await apiFetch(
        `${apiBase}/snapshots/${snapshotId}/file-changes${query ? `?${query}` : ""}`
      );
      if (!res.ok) {
        throw new Error("Failed to load file changes");
      }
      const data = await res.json();
      setFileChanges(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const fetchChangeTree = async (snapshotId) => {
    if (!snapshotId) {
      setChangeTree([]);
      return;
    }
    setTreeError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${snapshotId}/change-tree`);
      if (res.status === 404) {
        setChangeTree([]);
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load change tree");
      }
      const data = await res.json();
      setChangeTree(data.nodes ?? []);
    } catch (err) {
      setTreeError(err.message);
    }
  };

  const fetchSummary = async (sessionId) => {
    if (!sessionId) {
      setSummary(null);
      setSummaryStatus("idle");
      return;
    }
    setSummaryStatus("loading");
    setSummaryError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${sessionId}/summary`);
      if (res.status === 404) {
        setSummary(null);
        setSummaryStatus("empty");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load change summary");
      }
      const data = await res.json();
      setSummary(data);
      setSummaryStatus("ready");
    } catch (err) {
      setSummaryError(err.message);
      setSummaryStatus("error");
    }
  };

  const fetchFlowGraph = async (sessionId) => {
    if (!sessionId) {
      setFlowGraph({ nodes: [], edges: [] });
      setFlowStatus("idle");
      return;
    }
    setFlowStatus("loading");
    setFlowError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${sessionId}/flow`);
      if (res.status === 404) {
        setFlowGraph({ nodes: [], edges: [] });
        setFlowStatus("empty");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load flow graph");
      }
      const data = await res.json();
      setFlowGraph({ nodes: data.nodes ?? [], edges: data.edges ?? [] });
      setFlowStatus("ready");
    } catch (err) {
      setFlowError(err.message);
      setFlowStatus("error");
    }
  };

  const fetchTranscript = async (sessionId) => {
    if (!sessionId) {
      setTranscriptEntries([]);
      setTranscriptStatus("idle");
      setTranscriptError("");
      setTranscriptExportStatus("idle");
      return;
    }
    setTranscriptStatus("loading");
    setTranscriptError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${sessionId}/transcript`);
      if (res.status === 404) {
        setTranscriptEntries([]);
        setTranscriptStatus("ready");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load transcript");
      }
      const data = await res.json();
      setTranscriptEntries(data.entries ?? []);
      setTranscriptStatus("ready");
    } catch (err) {
      setTranscriptError(err.message);
      setTranscriptStatus("error");
    }
  };

  const exportTranscript = async (format) => {
    if (!selectedId) {
      return;
    }
    setTranscriptExportStatus("loading");
    setTranscriptError("");
    try {
      const res = await apiFetch(
        `${apiBase}/review-sessions/${selectedId}/transcript/export?format=${format}`
      );
      if (!res.ok) {
        throw new Error("Failed to export transcript");
      }
      const payload =
        format === "csv"
          ? await res.text()
          : JSON.stringify(await res.json(), null, 2);
      const type = format === "csv" ? "text/csv" : "application/json";
      const blob = new Blob([payload], { type });
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `transcript-${selectedId}.${format === "csv" ? "csv" : "json"}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setTranscriptError(err.message);
    } finally {
      setTranscriptExportStatus("idle");
    }
  };

  const fetchComplianceReport = async (sessionId, { updateState = true } = {}) => {
    if (!sessionId) {
      if (updateState) {
        setComplianceReport(null);
        setComplianceStatus("idle");
        setComplianceError("");
      }
      return null;
    }
    if (updateState) {
      setComplianceStatus("loading");
      setComplianceError("");
    }
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${sessionId}/compliance-report`);
      if (!res.ok) {
        throw new Error("Failed to load compliance report");
      }
      const data = await res.json();
      if (updateState) {
        setComplianceReport(data);
        setComplianceStatus("ready");
      }
      return data;
    } catch (err) {
      if (updateState) {
        setComplianceError(err.message);
        setComplianceStatus("error");
      }
      return null;
    }
  };

  const downloadComplianceReport = async () => {
    if (!selectedId) {
      return;
    }
    const report =
      complianceReport?.reviewSessionId === selectedId
        ? complianceReport
        : await fetchComplianceReport(selectedId);
    if (!report) {
      return;
    }
    const payload = JSON.stringify(report, null, 2);
    const blob = new Blob([payload], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `compliance-report-${selectedId}.json`;
    link.click();
    URL.revokeObjectURL(url);
  };

  const fetchOrgInsights = async (repoFilter = null) => {
    setInsightsStatus("loading");
    setInsightsError("");
    try {
      const params = new URLSearchParams();
      if (repoFilter) {
        params.set("repo", repoFilter);
      }
      const query = params.toString();
      const res = await apiFetch(`${apiBase}/insights/org${query ? `?${query}` : ""}`);
      if (!res.ok) {
        throw new Error("Failed to load insights");
      }
      const data = await res.json();
      setOrgInsights(data);
      setInsightsStatus("ready");
    } catch (err) {
      setInsightsError(err.message);
      setInsightsStatus("error");
    }
  };

  const buildReview = async () => {
    if (!selectedId) return;
    setBuildStatus("loading");
    setBuildError("");
    try {
      const res = await apiFetch(`${apiBase}/review-sessions/${selectedId}/build`, {
        method: "POST"
      });
      if (!res.ok) {
        throw new Error("Failed to build review");
      }
      await res.json();
      await fetchChangeTree(selectedId);
      await fetchSummary(selectedId);
      await fetchFlowGraph(selectedId);
      await fetchOrgInsights(insightsScope === "repo" ? selectedRepoFilter : null);
      setSelectedNode(null);
      setSelectedFlowNodeId(null);
      setBuildStatus("ready");
    } catch (err) {
      setBuildStatus("error");
      setBuildError(err.message);
    }
  };

  const fetchBehaviourSummary = async (nodeId) => {
    if (!nodeId) {
      setBehaviourSummary(null);
      setBehaviourStatus("idle");
      setBehaviourError("");
      return;
    }
    setBehaviourStatus("loading");
    setBehaviourError("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${nodeId}/behaviour-summary`);
      if (res.status === 404) {
        setBehaviourSummary(null);
        setBehaviourStatus("empty");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load behaviour summary");
      }
      const data = await res.json();
      setBehaviourSummary(data);
      setBehaviourStatus("ready");
    } catch (err) {
      setBehaviourError(err.message);
      setBehaviourStatus("error");
    }
  };

  const fetchChecklist = async (nodeId) => {
    if (!nodeId) {
      setChecklist([]);
      setChecklistStatus("idle");
      setChecklistError("");
      setChecklistDraft("");
      setChecklistAddStatus("idle");
      setChecklistAddError("");
      setChecklistDraft("");
      setChecklistAddStatus("idle");
      setChecklistAddError("");
      return;
    }
    setChecklistStatus("loading");
    setChecklistError("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${nodeId}/checklist`);
      if (res.status === 404) {
        setChecklist([]);
        setChecklistStatus("empty");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load checklist");
      }
      const data = await res.json();
      const normalizedItems = Array.isArray(data.items)
        ? data.items.map((item) =>
            typeof item === "string" ? { text: item, source: "heuristic" } : item
          )
        : [];
      setChecklist(normalizedItems);
      setChecklistStatus("ready");
    } catch (err) {
      setChecklistError(err.message);
      setChecklistStatus("error");
    }
  };

  const fetchReviewerQuestions = async (nodeId) => {
    if (!nodeId) {
      setReviewerQuestions([]);
      setQuestionsStatus("idle");
      setQuestionsError("");
      setQuestionsSource("");
      return;
    }
    setQuestionsStatus("loading");
    setQuestionsError("");
    setQuestionsSource("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${nodeId}/questions`);
      if (res.status === 404) {
        setReviewerQuestions([]);
        setQuestionsStatus("empty");
        return;
      }
      if (!res.ok) {
        throw new Error("Failed to load reviewer questions");
      }
      const data = await res.json();
      setReviewerQuestions(data.items ?? []);
      setQuestionsSource(data.source ?? "");
      setQuestionsStatus("ready");
    } catch (err) {
      setQuestionsError(err.message);
      setQuestionsStatus("error");
    }
  };

  const addChecklistItem = async (nodeId, text, source = "conversation") => {
    if (!nodeId || !text.trim()) {
      return;
    }
    setChecklistAddStatus("saving");
    setChecklistAddError("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${nodeId}/checklist/items`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ text: text.trim(), source })
      });
      if (!res.ok) {
        throw new Error("Failed to add checklist item");
      }
      const data = await res.json();
      const normalizedItems = Array.isArray(data.items)
        ? data.items.map((item) =>
            typeof item === "string" ? { text: item, source: "heuristic" } : item
          )
        : [];
      setChecklist(normalizedItems);
      setChecklistStatus("ready");
      setChecklistDraft("");
      setChecklistAddStatus("idle");
    } catch (err) {
      setChecklistAddError(err.message);
      setChecklistAddStatus("error");
    }
  };

  const sendChatMessage = async () => {
    if (!selectedId) {
      setChatError("Select a review session before starting a chat.");
      return;
    }
    if (!selectedNode) {
      setChatError("Select a review node to keep the chat scoped to PR changes.");
      return;
    }
    const questionText = chatDraft.trim();
    if (!questionText) {
      setChatError("Enter a question about the PR changes.");
      return;
    }
    setChatStatus("sending");
    setChatError("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${selectedNode.id}/voice-query`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question: questionText })
      });
      if (!res.ok) {
        throw new Error("Failed to send chat message");
      }
      await res.json();
      setChatDraft("");
      await addChecklistItem(selectedNode.id, questionText, "conversation");
      await fetchTranscript(selectedId);
      setChatStatus("idle");
    } catch (err) {
      setChatError(err.message);
      setChatStatus("error");
    }
  };

  const fetchDiff = async (snapshotId, path) => {
    if (!snapshotId || !path) {
      return;
    }
    setDiffStatus("loading");
    setDiffText("");
    try {
      const res = await apiFetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
      if (!res.ok) {
        throw new Error("Diff not available");
      }
      const text = await res.text();
      setDiffText(text);
      setDiffStatus("ready");
    } catch (err) {
      setDiffStatus("error");
      setDiffText(err.message);
    }
  };

  const fetchFullDiffForPath = async (snapshotId, path) => {
    if (!snapshotId || !path || fullDiffCache[path]) {
      return;
    }
    setFullDiffStatus("loading");
    setFullDiffError("");
    try {
      const res = await apiFetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
      if (!res.ok) {
        throw new Error("Diff not available");
      }
      const text = await res.text();
      setFullDiffCache((current) => ({ ...current, [path]: text }));
      setFullDiffStatus("ready");
    } catch (err) {
      setFullDiffError(err.message);
      setFullDiffStatus("error");
    }
  };

  const fetchAllFullDiffs = async (snapshotId, paths) => {
    if (!snapshotId || paths.length === 0) {
      return;
    }
    const missing = paths.filter((path) => !fullDiffCache[path]);
    if (missing.length === 0) {
      return;
    }
    setFullDiffStatus("loading");
    setFullDiffError("");
    try {
      const responses = await Promise.all(
        missing.map(async (path) => {
          const res = await apiFetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
          if (!res.ok) {
            throw new Error(`Diff not available for ${path}`);
          }
          const text = await res.text();
          return { path, text };
        })
      );
      setFullDiffCache((current) => {
        const next = { ...current };
        responses.forEach((item) => {
          next[item.path] = item.text;
        });
        return next;
      });
      setFullDiffStatus("ready");
    } catch (err) {
      setFullDiffError(err.message);
      setFullDiffStatus("error");
    }
  };

  const fetchNodeDiff = async (nodeId) => {
    if (!nodeId) {
      return;
    }
    setDiffStatus("loading");
    setDiffText("");
    try {
      const res = await apiFetch(`${apiBase}/review-nodes/${nodeId}/diff`);
      if (!res.ok) {
        throw new Error("Diff not available");
      }
      const text = await res.text();
      setDiffText(text);
      setDiffStatus("ready");
    } catch (err) {
      setDiffStatus("error");
      setDiffText(err.message);
    }
  };

  const handleDeleteSnapshot = async () => {
    if (!selectedId) return;
    const confirmed = window.confirm("Delete this review session? This cannot be undone.");
    if (!confirmed) return;
    setLoading(true);
    setError("");
    try {
      const res = await apiFetch(`${apiBase}/snapshots/${selectedId}`, { method: "DELETE" });
      if (!res.ok) {
        throw new Error("Failed to delete review session");
      }
      setSelectedId(null);
      setDetail(null);
      setFileChanges([]);
      setChangeTree([]);
      setSelectedChange(null);
      setSelectedNode(null);
      setSelectedFlowNodeId(null);
      setBehaviourSummary(null);
      setBehaviourStatus("idle");
      setBehaviourError("");
      setChecklist([]);
      setChecklistStatus("idle");
      setChecklistError("");
      setReviewerQuestions([]);
      setQuestionsStatus("idle");
      setQuestionsError("");
      setQuestionsSource("");
      setCenterTab("summary");
      setRightTab("review");
      setChatDraft("");
      setChatStatus("idle");
      setChatError("");
      setTranscriptEntries([]);
      setTranscriptStatus("idle");
      setTranscriptError("");
      setTranscriptExportStatus("idle");
      setFullDiffStatus("idle");
      setFullDiffError("");
      setFullDiffActivePath(null);
      setFullDiffMode("single");
      setFullDiffFilter("");
      setFullDiffCache({});
      setComplianceReport(null);
      setComplianceStatus("idle");
      setComplianceError("");
      setDiffText("");
      setDiffStatus("idle");
      setBuildStatus("idle");
      setBuildError("");
      setSummary(null);
      setSummaryStatus("idle");
      setSummaryError("");
      setFlowGraph({ nodes: [], edges: [] });
      setFlowStatus("idle");
      setFlowError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const activeSnapshotName = useMemo(() => {
    if (!detail) return "";
    return `${detail.owner}/${detail.name} · PR #${detail.prNumber}`;
  }, [detail]);

  const sessionKpis = useMemo(() => {
    const addedLines = fileChanges.reduce((total, item) => total + (item.addedLines ?? 0), 0);
    const removedLines = fileChanges.reduce((total, item) => total + (item.deletedLines ?? 0), 0);
    const totalChangedLines = addedLines + removedLines;
    const riskTagsCount = summary?.riskTags?.length ?? 0;
    const focusLabel = selectedNode?.label ? `Focus: ${selectedNode.label}` : "No active node";
    const completionLabel =
      buildStatus === "ready"
        ? "Review built"
        : buildStatus === "loading"
          ? "Building review"
          : "Build pending";

    return [
      {
        label: "Files",
        value: fileChanges.length,
        meta: detail ? `${detail.owner}/${detail.name}` : "No session loaded"
      },
      {
        label: "Changed Lines",
        value: totalChangedLines,
        meta: `+${addedLines} / -${removedLines}`
      },
      {
        label: "Risk Tags",
        value: riskTagsCount,
        meta: summaryStatus === "ready" ? "From AI summary" : "Build review to detect"
      },
      {
        label: "Review Nodes",
        value: changeTree.length,
        meta: `${completionLabel} · ${focusLabel}`
      }
    ];
  }, [buildStatus, changeTree.length, detail, fileChanges, selectedNode, summary, summaryStatus]);

  const activeDashboardLayout = useMemo(
    () => dashboardLayouts.find((layout) => layout.id === dashboardLayout) ?? dashboardLayouts[0],
    [dashboardLayout]
  );

  const selectedRepoFilter = useMemo(() => {
    if (!detail?.owner || !detail?.name) {
      return null;
    }
    return `${detail.owner}/${detail.name}`;
  }, [detail]);

  useEffect(() => {
    if (activeView !== "insights") {
      return;
    }
    if (insightsScope === "repo" && !selectedRepoFilter) {
      setOrgInsights(null);
      setInsightsStatus("idle");
      setInsightsError("");
      return;
    }
    fetchOrgInsights(insightsScope === "repo" ? selectedRepoFilter : null);
  }, [activeView, insightsScope, selectedRepoFilter]);

  useEffect(() => {
    if (selectedId) {
      setSelectedChange(null);
      setSelectedNode(null);
      setSelectedFlowNodeId(null);
      setBehaviourSummary(null);
      setBehaviourStatus("idle");
      setBehaviourError("");
      setChecklist([]);
      setChecklistStatus("idle");
      setChecklistError("");
      setReviewerQuestions([]);
      setQuestionsStatus("idle");
      setQuestionsError("");
      setQuestionsSource("");
      setChatDraft("");
      setChatStatus("idle");
      setChatError("");
      setTranscriptEntries([]);
      setTranscriptStatus("idle");
      setTranscriptError("");
      setTranscriptExportStatus("idle");
      setComplianceReport(null);
      setComplianceStatus("idle");
      setComplianceError("");
      fetchDetail(selectedId);
      fetchFileChanges(selectedId);
      fetchChangeTree(selectedId);
      fetchSummary(selectedId);
      fetchFlowGraph(selectedId);
      fetchTranscript(selectedId);
    }
  }, [selectedId]);

  useEffect(() => {
    if (!autoResolveLatest || !detail) {
      return;
    }
    resolveLatestSession({
      owner: detail.owner,
      name: detail.name,
      prNumber: detail.prNumber
    });
    setAutoResolveLatest(false);
  }, [autoResolveLatest, detail]);

  useEffect(() => {
    if (selectedId) {
      fetchFileChanges(selectedId);
    }
  }, [changeTypeFilter, pathPrefixFilter, selectedId]);

  useEffect(() => {
    if (centerTab !== "fullDiff") {
      return;
    }
    if (!fullDiffActivePath && fileChanges.length > 0) {
      setFullDiffActivePath(fileChanges[0].path);
    }
  }, [centerTab, fileChanges, fullDiffActivePath]);

  useEffect(() => {
    if (centerTab !== "fullDiff" || !selectedId) {
      return;
    }
    const paths = fileChanges.map((change) => change.path);
    if (fullDiffMode === "all") {
      fetchAllFullDiffs(selectedId, paths);
      return;
    }
    if (fullDiffActivePath) {
      fetchFullDiffForPath(selectedId, fullDiffActivePath);
    }
  }, [centerTab, fullDiffMode, fullDiffActivePath, fileChanges, selectedId]);

  useEffect(() => {
    if (selectedChange && selectedId) {
      fetchDiff(selectedId, selectedChange.path);
    }
  }, [selectedChange, selectedId]);

  useEffect(() => {
    if (selectedNode) {
      setSelectedFlowNodeId(selectedNode.id);
      if (selectedNode.nodeType === "file") {
        fetchBehaviourSummary(selectedNode.id);
        fetchChecklist(selectedNode.id);
        fetchReviewerQuestions(selectedNode.id);
      } else {
        setBehaviourSummary(null);
        setBehaviourStatus("idle");
        setChecklist([]);
        setChecklistStatus("idle");
        setChecklistDraft("");
        setChecklistAddStatus("idle");
        setChecklistAddError("");
        setReviewerQuestions([]);
        setQuestionsStatus("idle");
        setQuestionsError("");
        setQuestionsSource("");
      }
      if (selectedNode.path) {
        fetchNodeDiff(selectedNode.id);
      }
    }
  }, [selectedNode, selectedId]);

  const treeLookup = useMemo(() => {
    const byParent = new Map();
    const rank = {
      group: 0,
      file: 1,
      summary: 2,
      entry_point: 3,
      risk: 4,
      side_effect: 5,
      checklist: 6,
      hunk: 7
    };
    changeTree.forEach((node) => {
      const key = node.parentId ?? "root";
      if (!byParent.has(key)) {
        byParent.set(key, []);
      }
      byParent.get(key).push(node);
    });
    byParent.forEach((nodes) => {
      nodes.sort((a, b) => {
        const rankA = rank[a.nodeType] ?? 99;
        const rankB = rank[b.nodeType] ?? 99;
        if (rankA !== rankB) {
          return rankA - rankB;
        }
        return a.label.localeCompare(b.label);
      });
    });
    return byParent;
  }, [changeTree]);

  const selectedNodePath = useMemo(() => {
    return selectedNode?.path ?? null;
  }, [selectedNode]);

  const changeTreeById = useMemo(() => {
    return new Map(changeTree.map((node) => [node.id, node]));
  }, [changeTree]);

  const transcriptDisplay = useMemo(() => {
    return transcriptEntries.map((entry) => ({
      ...entry,
      nodeLabel: changeTreeById.get(entry.reviewNodeId)?.label ?? "Unknown node"
    }));
  }, [changeTreeById, transcriptEntries]);

  const chatStatusLabel = useMemo(() => {
    if (chatStatus === "sending") {
      return "Sending your question…";
    }
    if (chatStatus === "error") {
      return "Needs attention";
    }
    return "Ready";
  }, [chatStatus]);

  const parseDiffLines = (text) => {
    if (!text) {
      return [];
    }
    let normalizedText = text;
    if (!normalizedText.includes("\n") && normalizedText.includes("\\n")) {
      normalizedText = normalizedText
        .replace(/\\r\\n/g, "\n")
        .replace(/\\n/g, "\n")
        .replace(/\\t/g, "\t");
    }
    const lines = normalizedText.replace(/\r\n/g, "\n").split("\n");
    let oldLine = null;
    let newLine = null;
    const parsed = [];

    lines.forEach((line) => {
      if (line.startsWith("@@")) {
        const match = line.match(/@@ -(\d+)(?:,\d+)? \+(\d+)(?:,\d+)? @@/);
        oldLine = match ? Number(match[1]) : null;
        newLine = match ? Number(match[2]) : null;
        parsed.push({ type: "hunk", text: line, oldNumber: "", newNumber: "" });
        return;
      }

      if (line.startsWith("diff --git")) {
        parsed.push({ type: "file-header", text: line, oldNumber: "", newNumber: "" });
        return;
      }

      if (line.startsWith("+++ ") || line.startsWith("--- ")) {
        parsed.push({ type: "file", text: line, oldNumber: "", newNumber: "" });
        return;
      }

      if (line.startsWith("index ") || line.startsWith("new file mode")) {
        parsed.push({ type: "meta", text: line, oldNumber: "", newNumber: "" });
        return;
      }

      if (line.startsWith("\\")) {
        parsed.push({ type: "meta", text: line, oldNumber: "", newNumber: "" });
        return;
      }

      if (line.startsWith("+")) {
        parsed.push({
          type: "add",
          text: line,
          oldNumber: "",
          newNumber: newLine ?? ""
        });
        if (typeof newLine === "number") {
          newLine += 1;
        }
        return;
      }

      if (line.startsWith("-")) {
        parsed.push({
          type: "del",
          text: line,
          oldNumber: oldLine ?? "",
          newNumber: ""
        });
        if (typeof oldLine === "number") {
          oldLine += 1;
        }
        return;
      }

      parsed.push({
        type: "context",
        text: line,
        oldNumber: oldLine ?? "",
        newNumber: newLine ?? ""
      });
      if (typeof oldLine === "number") {
        oldLine += 1;
      }
      if (typeof newLine === "number") {
        newLine += 1;
      }
    });

    return parsed;
  };

  const diffLines = useMemo(() => parseDiffLines(diffText), [diffText]);

  const fullDiffText = useMemo(() => {
    if (fullDiffMode === "all") {
      const paths = fileChanges.map((change) => change.path);
      const missing = paths.some((path) => !fullDiffCache[path]);
      if (missing) {
        return "";
      }
      return paths.map((path) => fullDiffCache[path]).filter(Boolean).join("\n");
    }
    if (fullDiffActivePath) {
      return fullDiffCache[fullDiffActivePath] ?? "";
    }
    return "";
  }, [fileChanges, fullDiffActivePath, fullDiffCache, fullDiffMode]);

  const fullDiffLines = useMemo(() => parseDiffLines(fullDiffText), [fullDiffText]);

  const diffStats = useMemo(() => {
    const added = diffLines.filter((line) => line.type === "add").length;
    const removed = diffLines.filter((line) => line.type === "del").length;
    return { added, removed };
  }, [diffLines]);

  const fullDiffStats = useMemo(() => {
    if (fullDiffMode === "all") {
      const added = fileChanges.reduce((total, change) => total + (change.addedLines || 0), 0);
      const removed = fileChanges.reduce((total, change) => total + (change.deletedLines || 0), 0);
      return { added, removed };
    }
    const active = fileChanges.find((change) => change.path === fullDiffActivePath);
    return {
      added: active?.addedLines ?? 0,
      removed: active?.deletedLines ?? 0
    };
  }, [fileChanges, fullDiffActivePath, fullDiffMode]);

  const checklistGroups = useMemo(() => {
    const groups = checklist.reduce((acc, item) => {
      const key = item.source || "heuristic";
      if (!acc[key]) {
        acc[key] = [];
      }
      acc[key].push(item);
      return acc;
    }, {});
    const order = ["conversation", "llm", "heuristic"];
    return Object.keys(groups)
      .sort((a, b) => {
        const indexA = order.indexOf(a);
        const indexB = order.indexOf(b);
        if (indexA === -1 && indexB === -1) return a.localeCompare(b);
        if (indexA === -1) return 1;
        if (indexB === -1) return -1;
        return indexA - indexB;
      })
      .map((key) => ({ source: key, items: groups[key] }));
  }, [checklist]);

  const insightsData = useMemo(() => {
    const repoItems = orgInsights?.repositories ?? [];
    const riskItems = orgInsights?.riskTags ?? [];
    const hotItems = orgInsights?.hotPaths ?? [];
    const volumeItems = orgInsights?.reviewVolume ?? [];
    return {
      repoItems,
      riskItems,
      hotItems,
      volumeItems,
      volumeDisplay: volumeItems.slice(-7),
      maxRepo: Math.max(1, ...repoItems.map((item) => item.reviewSessionCount)),
      maxRisk: Math.max(1, ...riskItems.map((item) => item.count)),
      maxHot: Math.max(1, ...hotItems.map((item) => item.count)),
      maxVolume: Math.max(1, ...volumeItems.map((item) => item.count))
    };
  }, [orgInsights]);

  const insightsSummary = useMemo(() => {
    const sessions = orgInsights?.reviewSessionCount ?? 0;
    const summaries = orgInsights?.reviewSummaryCount ?? 0;
    const repoCount = orgInsights?.repositories?.length ?? 0;
    const coverage = sessions > 0 ? Math.round((summaries / sessions) * 100) : 0;
    const averagePerRepo = repoCount > 0 ? (sessions / repoCount).toFixed(1) : "0.0";
    const peakDay = (insightsData.volumeItems ?? []).reduce(
      (current, item) => (!current || item.count > current.count ? item : current),
      null
    );
    return {
      sessions,
      summaries,
      repoCount,
      coverage,
      averagePerRepo,
      peakDay
    };
  }, [orgInsights, insightsData]);

  const activeFlowState = useMemo(() => {
    if (!selectedFlowNodeId) {
      return { activeNodes: new Set(), activeEdges: new Set() };
    }

    const edges = flowGraph.edges ?? [];
    const nodeLookup = new Map((flowGraph.nodes ?? []).map((node) => [node.id, node]));
    const outgoing = new Map();
    const incoming = new Map();

    edges.forEach((edge) => {
      if (!outgoing.has(edge.sourceId)) {
        outgoing.set(edge.sourceId, []);
      }
      if (!incoming.has(edge.targetId)) {
        incoming.set(edge.targetId, []);
      }
      outgoing.get(edge.sourceId).push(edge);
      incoming.get(edge.targetId).push(edge);
    });

    const activeEdges = new Set();
    const activeNodes = new Set([selectedFlowNodeId]);

    const addEdge = (edge) => {
      activeEdges.add(edge.id);
      activeNodes.add(edge.sourceId);
      activeNodes.add(edge.targetId);
    };

    (outgoing.get(selectedFlowNodeId) ?? []).forEach(addEdge);
    (incoming.get(selectedFlowNodeId) ?? []).forEach(addEdge);

    const selectedNode = nodeLookup.get(selectedFlowNodeId);
    if (selectedNode?.nodeType === "entry") {
      (outgoing.get(selectedFlowNodeId) ?? []).forEach((edge) => {
        (outgoing.get(edge.targetId) ?? []).forEach(addEdge);
      });
    }

    if (selectedNode?.nodeType === "side_effect") {
      (incoming.get(selectedFlowNodeId) ?? []).forEach((edge) => {
        (incoming.get(edge.sourceId) ?? []).forEach(addEdge);
      });
    }

    return { activeNodes, activeEdges };
  }, [flowGraph, selectedFlowNodeId]);

  const extractRiskSeverity = (evidence = []) => {
    const match = evidence.find((item) => item.toLowerCase().startsWith("riskseverity:"));
    if (!match) return "low";
    const parts = match.split(":");
    return parts[1]?.trim() || "low";
  };

  const hasRiskEvidence = (evidence = []) => {
    return evidence.some((item) => item.toLowerCase().startsWith("risk:"));
  };

  const flowLayout = useMemo(() => {
    const nodes = flowGraph.nodes ?? [];
    const edges = flowGraph.edges ?? [];
    const grouped = new Map(flowColumns.map((key) => [key, []]));

    nodes.forEach((node) => {
      const key = flowColumns.includes(node.nodeType) ? node.nodeType : "other";
      grouped.get(key).push(node);
    });

    const layoutNodes = [];
    flowColumns.forEach((column) => {
      const group = grouped.get(column) ?? [];
      group.forEach((node, index) => {
        const evidence = node.evidence ?? [];
        const severity = extractRiskSeverity(evidence);
        const isRisk = hasRiskEvidence(evidence);
        const isActive = activeFlowState.activeNodes.has(node.id);
        const riskClass = isRisk ? `flow-node--risk flow-node--risk-${severity}` : "";
        const activeClass = isActive ? "flow-node--active" : "";
        layoutNodes.push({
          id: node.id,
          data: { label: node.label, type: node.nodeType, evidence },
          position: { x: flowColumnX[column], y: index * 90 },
          className: `flow-node flow-node--${node.nodeType} ${riskClass} ${activeClass}`.trim()
        });
      });
    });

    const layoutEdges = edges.map((edge) => ({
      id: edge.id,
      source: edge.sourceId,
      target: edge.targetId,
      label: edge.label,
      animated: activeFlowState.activeEdges.has(edge.id),
      className: `flow-edge flow-edge--${edge.label} ${
        activeFlowState.activeEdges.has(edge.id) ? "flow-edge--active" : ""
      }`.trim()
    }));

    return { nodes: layoutNodes, edges: layoutEdges };
  }, [flowGraph, activeFlowState]);

  const renderTree = (parentId = "root", depth = 0) => {
    const nodes = treeLookup.get(parentId) ?? [];
    return nodes.map((node) => (
      <div key={node.id} className="tree__node" style={{ paddingLeft: `${depth * 16}px` }}>
        <button
          className={`tree__button ${selectedNode?.id === node.id ? "tree__button--active" : ""}`}
          onClick={() => {
            setSelectedNode(node);
            setSelectedChange(null);
            setSelectedFlowNodeId(node.id);
          }}
        >
          <span className={`tree__type tree__type--${node.nodeType}`}>{node.nodeType}</span>
          <span className="tree__label">{node.label}</span>
          {node.riskSeverity && node.riskSeverity !== "low" && (
            <span className={`tree__severity tree__severity--${node.riskSeverity}`}>
              {node.riskSeverity}
            </span>
          )}
          {node.riskTags?.length > 0 && (
            <span className="tree__meta">{node.riskTags.join(", ")}</span>
          )}
        </button>
        {renderTree(node.id, depth + 1)}
      </div>
    ));
  };

  const handleFlowNodeClick = (_, node) => {
    const reviewNode = changeTreeById.get(node.id);
    if (reviewNode) {
      setSelectedNode(reviewNode);
      setSelectedChange(null);
      setSelectedFlowNodeId(node.id);
      return;
    }
    setSelectedFlowNodeId(node.id);
  };

  const renderSetup = () => {
    return (
      <section className="setup">
        <div className="setup__hero">
          <div>
            <span className="setup__eyebrow">GitHub App Setup</span>
            <h2>Connect repositories once, then ingest every pull request.</h2>
            <p>
              Yotei uses a GitHub App for secure, org-friendly access. Install it once, then let
              webhook events keep review sessions in sync.
            </p>
            <div
              className={`setup__callout ${
                tenantToken ? "setup__callout--success" : "setup__callout--warning"
              }`}
            >
              <div className="setup__token-row">
                <div>
                  <strong>
                    {tenantToken ? "Tenant connected" : "Tenant not connected"}
                  </strong>
                  <p>
                    {tenantToken
                      ? "This browser is linked to a tenant token for API access."
                      : "Install the GitHub App to receive a tenant token and unlock the dashboard."}
                  </p>
                </div>
                {tenantToken && (
                  <button className="button ghost" onClick={handleTenantReset}>
                    Disconnect
                  </button>
                )}
              </div>
            </div>
            {!isAdminSetup && (
              <div className="setup__cta">
                <p>
                  You do not need Render access. Install the GitHub App and pick the repositories to
                  monitor. You will be redirected back with your tenant token.
                </p>
                {installUrl ? (
                  <a className="button" href={installUrl} target="_blank" rel="noreferrer">
                    Install GitHub App
                  </a>
                ) : (
                  <div className="setup__callout">
                    Ask your Yotei admin for the GitHub App install link.
                  </div>
                )}
              </div>
            )}
            <div className="setup__pill-row">
              <div className="setup__pill">
                <span>Webhook</span>
                <strong>{webhookUrl}</strong>
              </div>
              <div className="setup__pill">
                <span>API</span>
                <strong>{normalizedApiBase}</strong>
              </div>
            </div>
          </div>
          <div className="setup__signal card">
            <div className="setup__signal-header">
              <div>
                <p>Deploy status</p>
                <h3>Render + GitHub App</h3>
              </div>
              <span className="setup__badge">beta</span>
            </div>
            <div className="setup__signal-body">
              <div>
                <span>Webhook endpoint</span>
                <strong>{webhookUrl}</strong>
              </div>
              <div>
                <span>Manual ingest</span>
                <strong>{ingestUrl}</strong>
              </div>
              <div>
                <span>Sync open PRs</span>
                <strong>{syncUrl}</strong>
              </div>
            </div>
            <div className="setup__signal-actions">
              <button
                className="button ghost"
                onClick={() => handleCopy(webhookUrl, "webhook")}
              >
                {copiedLabel === "webhook" ? "Copied" : "Copy webhook"}
              </button>
              <a className="button" href={`${normalizedApiBase}/health`} target="_blank" rel="noreferrer">
                Check API health
              </a>
            </div>
          </div>
        </div>

        <div className="setup__grid">
          <article className="card setup__step" style={{ "--delay": "0.05s" }}>
            <div className="setup__step-header">
              <span className="setup__step-index">01</span>
              <div>
                <h3>{isAdminSetup ? "Create the GitHub App" : "Install the GitHub App"}</h3>
                <p>
                  {isAdminSetup
                    ? "Give Yotei read access to pull requests."
                    : "Authorize access to the repos you want to monitor."}
                </p>
              </div>
            </div>
            <ul className="setup__list">
              {isAdminSetup ? (
                <>
                  <li>Permissions: Contents (read), Pull requests (read).</li>
                  <li>Subscribe to pull_request events.</li>
                  <li>Generate a private key for the app.</li>
                </>
              ) : (
                <>
                  <li>Select the repos you want Yotei to watch.</li>
                  <li>Confirm the installation for your org.</li>
                  <li>Open or update a PR to trigger ingestion.</li>
                </>
              )}
            </ul>
          </article>

          <article className="card setup__step" style={{ "--delay": "0.1s" }}>
            <div className="setup__step-header">
              <span className="setup__step-index">02</span>
              <div>
                <h3>{isAdminSetup ? "Install on a repo or org" : "Confirm the install"}</h3>
                <p>
                  {isAdminSetup
                    ? "Choose which repos Yotei should watch and configure the callback."
                    : "Keep the installation connected for ongoing sync."}
                </p>
              </div>
            </div>
            <ul className="setup__list">
              {isAdminSetup ? (
                <>
                  <li>Set the setup callback URL to {`${normalizedApiBase}/github/install`}.</li>
                  <li>Install the app on the org or repos you want to monitor.</li>
                </>
              ) : (
                <>
                  <li>Make sure the app is installed on the right repos.</li>
                  <li>Let your admin know if anything looks missing.</li>
                </>
              )}
            </ul>
          </article>

          {isAdminSetup ? (
            <>
              <article className="card setup__step" style={{ "--delay": "0.15s" }}>
                <div className="setup__step-header">
                  <span className="setup__step-index">03</span>
                  <div>
                    <h3>Set environment variables</h3>
                    <p>Use PEM or base64 for the private key.</p>
                  </div>
                </div>
                <pre className="setup__code">{`GitHub__App__AppId=...
GitHub__App__PrivateKey=...
GitHub__App__WebhookSecret=...
Frontend__BaseUrl=...
VITE_GITHUB_APP_INSTALL_URL=...`}</pre>
                <div className="setup__code-actions">
                  <button
                    className="button ghost"
                    onClick={() =>
                      handleCopy(
                        `GitHub__App__AppId=\nGitHub__App__PrivateKey=\nGitHub__App__WebhookSecret=\nFrontend__BaseUrl=\nVITE_GITHUB_APP_INSTALL_URL=`,
                        "env"
                      )
                    }
                  >
                    {copiedLabel === "env" ? "Copied" : "Copy template"}
                  </button>
                </div>
              </article>

              <article className="card setup__step" style={{ "--delay": "0.2s" }}>
                <div className="setup__step-header">
                  <span className="setup__step-index">04</span>
                  <div>
                    <h3>Configure webhooks + callbacks</h3>
                    <p>Point GitHub to the ingestion and setup endpoints.</p>
                  </div>
                </div>
                <ul className="setup__list">
                  <li>Webhook URL: {webhookUrl}</li>
                  <li>Setup URL: {`${normalizedApiBase}/github/install`}</li>
                  <li>Content type: application/json</li>
                  <li>Secret: same as GitHub__App__WebhookSecret</li>
                </ul>
              </article>
            </>
          ) : (
            <article className="card setup__step" style={{ "--delay": "0.15s" }}>
              <div className="setup__step-header">
                <span className="setup__step-index">03</span>
                <div>
                  <h3>Open a pull request</h3>
                  <p>Yotei will ingest the PR automatically.</p>
                </div>
              </div>
              <ul className="setup__list">
                <li>Push commits to your PR to trigger sync.</li>
                <li>Open the PR comment link to load the session.</li>
              </ul>
            </article>
          )}
        </div>

        <div className="setup__footer card">
          <div>
            <h3>Test it fast</h3>
            <p>Open or update a PR, then use the PR comment link to view the review session.</p>
          </div>
          {isAdminSetup ? (
            <div className="setup__footer-actions">
              <button
                className="button ghost"
                onClick={() =>
                  handleCopy(
                    `curl -X POST "${syncUrl}" -H "X-Tenant-Token: <token>"`,
                    "curl-sync"
                  )
                }
              >
                {copiedLabel === "curl-sync" ? "Copied" : "Copy sync curl"}
              </button>
              <button
                className="button"
                onClick={() =>
                  handleCopy(
                    `curl -X POST "${ingestUrl}" -H "Content-Type: application/json" -H "X-Tenant-Token: <token>" -d '{"owner":"ORG","name":"REPO","prNumber":123}'`,
                    "curl-one"
                  )
                }
              >
                {copiedLabel === "curl-one" ? "Copied" : "Copy single PR curl"}
              </button>
            </div>
          ) : (
            <div className="setup__footer-actions">
              <button
                className="button ghost"
                onClick={() => handleCopy(webhookUrl, "webhook")}
              >
                {copiedLabel === "webhook" ? "Copied" : "Copy webhook"}
              </button>
            </div>
          )}
        </div>
      </section>
    );
  };

  const renderInsights = () => {
    const summaryLabel =
      insightsSummary.sessions > 0
        ? `${insightsSummary.summaries} of ${insightsSummary.sessions} sessions summarized`
        : "No review sessions yet.";
    const peakDayLabel = insightsSummary.peakDay
      ? `${new Date(insightsSummary.peakDay.date).toLocaleDateString()} · ${
          insightsSummary.peakDay.count
        } reviews`
      : "No review volume yet.";

    return (
      <main className="insights-page">
        <section className="card insights-hero">
          <div className="insights-hero__top">
            <div>
              <span className="insights-hero__eyebrow">Org Insights</span>
              <h2>Signals across review sessions.</h2>
              <p>
                Track where AI-generated changes are landing, which risks recur, and how review
                volume shifts across your org.
              </p>
            </div>
            <div className="insights-hero__actions">
              <div className="insights__actions">
                <button
                  className={`button ghost insights__toggle ${
                    insightsScope === "org" ? "insights__toggle--active" : ""
                  }`}
                  onClick={() => setInsightsScope("org")}
                >
                  Org
                </button>
                <button
                  className={`button ghost insights__toggle ${
                    insightsScope === "repo" ? "insights__toggle--active" : ""
                  }`}
                  onClick={() => setInsightsScope("repo")}
                  disabled={!detail}
                >
                  Repo
                </button>
              </div>
              <div className="insights-hero__meta">
                {insightsScope === "repo" && detail && (
                  <span className="pill">
                    {detail.owner}/{detail.name}
                  </span>
                )}
                {(orgInsights?.from || orgInsights?.to) && (
                  <>
                    {orgInsights?.from && (
                      <span className="pill">
                        From {new Date(orgInsights.from).toLocaleDateString()}
                      </span>
                    )}
                    {orgInsights?.to && (
                      <span className="pill">To {new Date(orgInsights.to).toLocaleDateString()}</span>
                    )}
                  </>
                )}
              </div>
            </div>
          </div>
          <div className="insights-metrics">
            <div className="insights-orb" style={{ "--ratio": insightsSummary.coverage }}>
              <div className="insights-orb__ring" />
              <div className="insights-orb__value">{insightsSummary.coverage}%</div>
              <div className="insights-orb__label">Summary coverage</div>
              <div className="insights-orb__sub">{summaryLabel}</div>
            </div>
            <div className="insights-metric">
              <div className="summary__label">Sessions</div>
              <div className="summary__value">{insightsSummary.sessions}</div>
              <div className="summary__sub">{insightsSummary.summaries} summarized</div>
            </div>
            <div className="insights-metric">
              <div className="summary__label">Repositories</div>
              <div className="summary__value">{insightsSummary.repoCount}</div>
              <div className="summary__sub">{insightsSummary.averagePerRepo} avg sessions</div>
            </div>
            <div className="insights-metric insights-metric--spark">
              <div className="summary__label">Review volume</div>
              <div className="insights-spark">
                {insightsData.volumeDisplay.map((item) => (
                  <span
                    key={item.date}
                    style={{
                      height: `${(item.count / insightsData.maxVolume) * 100}%`
                    }}
                  />
                ))}
              </div>
              <div className="summary__sub">{peakDayLabel}</div>
            </div>
          </div>
        </section>

        {insightsError && <div className="alert">{insightsError}</div>}
        {insightsStatus === "loading" && (
          <section className="card insights-state">Loading insights...</section>
        )}
        {insightsStatus === "idle" && insightsScope === "repo" && !detail && (
          <section className="card insights-state">
            Open a review session to scope insights to a repository.
          </section>
        )}
        {insightsStatus === "ready" && orgInsights && (
          <div className="insights-grid">
            <section className="card insights-panel">
              <div className="insights-panel__header">
                <div>
                  <h3>Repository activity</h3>
                  <p>Where review sessions are clustering right now.</p>
                </div>
                <span className="badge">{insightsData.repoItems.length}</span>
              </div>
              {insightsData.repoItems.length === 0 && (
                <p className="summary__empty">No repository activity yet.</p>
              )}
              {insightsData.repoItems.length > 0 && (
                <div className="insights-barlist">
                  {insightsData.repoItems.slice(0, 6).map((item) => (
                    <div key={`${item.owner}/${item.name}`} className="insights-bar">
                      <div className="insights-bar__label">
                        {item.owner}/{item.name}
                      </div>
                      <div className="insights-bar__track">
                        <span
                          style={{
                            width: `${(item.reviewSessionCount / insightsData.maxRepo) * 100}%`
                          }}
                        />
                      </div>
                      <div className="insights-bar__value">{item.reviewSessionCount}</div>
                    </div>
                  ))}
                </div>
              )}
            </section>

            <section className="card insights-panel">
              <div className="insights-panel__header">
                <div>
                  <h3>Risk tags</h3>
                  <p>Recurring risk signals across reviews.</p>
                </div>
                <span className="badge">{insightsData.riskItems.length}</span>
              </div>
              {insightsData.riskItems.length === 0 && (
                <p className="summary__empty">No risk tags yet.</p>
              )}
              {insightsData.riskItems.length > 0 && (
                <div className="insights-bubbles">
                  {insightsData.riskItems.slice(0, 8).map((item) => (
                    <div
                      key={item.label}
                      className="insights-bubble"
                      style={{
                        "--size": Math.max(0.35, item.count / insightsData.maxRisk)
                      }}
                    >
                      <span>{item.label}</span>
                      <strong>{item.count}</strong>
                    </div>
                  ))}
                </div>
              )}
            </section>

            <section className="card insights-panel">
              <div className="insights-panel__header">
                <div>
                  <h3>Hot paths</h3>
                  <p>Files and folders that attract the most attention.</p>
                </div>
                <span className="badge">{insightsData.hotItems.length}</span>
              </div>
              {insightsData.hotItems.length === 0 && (
                <p className="summary__empty">No hot paths yet.</p>
              )}
              {insightsData.hotItems.length > 0 && (
                <div className="insights-heat">
                  {insightsData.hotItems.slice(0, 6).map((item) => (
                    <div key={item.label} className="insights-heat__row">
                      <span className="insights-heat__label">{item.label}</span>
                      <div className="insights-heat__track">
                        <span
                          style={{ width: `${(item.count / insightsData.maxHot) * 100}%` }}
                        />
                      </div>
                      <span className="insights-heat__value">{item.count}</span>
                    </div>
                  ))}
                </div>
              )}
            </section>

            <section className="card insights-panel insights-panel--wide">
              <div className="insights-panel__header">
                <div>
                  <h3>Review volume</h3>
                  <p>Daily review sessions over the last week.</p>
                </div>
                <span className="badge">{insightsData.volumeItems.length}</span>
              </div>
              {insightsData.volumeDisplay.length === 0 && (
                <p className="summary__empty">No review volume yet.</p>
              )}
              {insightsData.volumeDisplay.length > 0 && (
                <div className="insights-volume">
                  {insightsData.volumeDisplay.map((item) => (
                    <div key={item.date} className="insights-volume__item">
                      <div className="insights-volume__bar">
                        <span
                          style={{
                            height: `${(item.count / insightsData.maxVolume) * 100}%`
                          }}
                        />
                      </div>
                      <div className="insights-volume__label">
                        {new Date(item.date).toLocaleDateString()}
                      </div>
                      <div className="insights-volume__value">{item.count}</div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          </div>
        )}
      </main>
    );
  };

  return (
    <div className={`app app--layout-${dashboardLayout}`}>
      {activeView !== "dashboard" && (
        <header className="app__header">
          <div>
            <h1>Yotei</h1>
            <p>{activeDashboardLayout.description}</p>
          </div>
          <div className="app__actions">
            <div className="view-toggle">
              <button
                className={`button ghost ${activeView === "dashboard" ? "button--active" : ""}`}
                onClick={() => setActiveView("dashboard")}
              >
                Dashboard
              </button>
              <button
                className={`button ghost ${activeView === "insights" ? "button--active" : ""}`}
                onClick={() => setActiveView("insights")}
              >
                Insights
              </button>
              <button
                className={`button ghost ${activeView === "setup" ? "button--active" : ""}`}
                onClick={() => setActiveView("setup")}
              >
                Setup
              </button>
            </div>
            {activeView === "insights" ? (
              <button
                className="button ghost"
                onClick={() =>
                  fetchOrgInsights(insightsScope === "repo" ? selectedRepoFilter : null)
                }
                disabled={
                  insightsStatus === "loading" ||
                  (insightsScope === "repo" && !selectedRepoFilter)
                }
              >
                {insightsStatus === "loading" ? "Refreshing..." : "Refresh Insights"}
              </button>
            ) : (
              <a
                className="button ghost"
                href={normalizedApiBase}
                target="_blank"
                rel="noreferrer"
              >
                Open API
              </a>
            )}
          </div>
        </header>
      )}
      {activeView === "setup" ? (
        renderSetup()
      ) : activeView === "insights" ? (
        renderInsights()
      ) : (
        <div className={`dashboard-shell dashboard-shell--${dashboardLayout}`}>
          <aside className="dashboard-sidebar">
            <div className="dashboard-sidebar__brand">
              <strong>Yotei</strong>
              <span>Admin Suite</span>
            </div>
            <p className="dashboard-sidebar__group-label">Main menu</p>
            <nav className="dashboard-sidebar__nav">
              {dashboardPrimaryNav.map((item) => (
                <button
                  key={item.key}
                  className={`dashboard-nav-item ${
                    item.key === "dashboard" ? "dashboard-nav-item--active" : ""
                  }`}
                >
                  <span className="dashboard-nav-item__icon">{item.short}</span>
                  <span>{item.label}</span>
                </button>
              ))}
            </nav>
            <p className="dashboard-sidebar__group-label">Other</p>
            <nav className="dashboard-sidebar__nav">
              {dashboardSecondaryNav.map((item) => (
                <button key={item.key} className="dashboard-nav-item">
                  <span className="dashboard-nav-item__icon">{item.short}</span>
                  <span>{item.label}</span>
                </button>
              ))}
            </nav>
            <button className="dashboard-sidebar__logout">Logout</button>
          </aside>
          <div className="dashboard-stage">
            <header className="dashboard-topbar">
              <div className="dashboard-topbar__intro">
                <h1>Dashboard</h1>
                <p>{activeDashboardLayout.description}</p>
              </div>
              <label className="dashboard-search">
                <span>Search</span>
                <input placeholder="Search sessions, files, nodes" />
              </label>
              <div className="dashboard-topbar__actions">
                <div className="dashboard-icon-strip">
                  <button className="dashboard-icon-strip__button">AL</button>
                  <button className="dashboard-icon-strip__button">NT</button>
                  <button className="dashboard-icon-strip__button">MS</button>
                </div>
                <div className="view-toggle">
                  <button className="button ghost button--active">Dashboard</button>
                  <button className="button ghost" onClick={() => setActiveView("insights")}>
                    Insights
                  </button>
                  <button className="button ghost" onClick={() => setActiveView("setup")}>
                    Setup
                  </button>
                </div>
                <div className="layout-switcher" role="group" aria-label="Dashboard style">
                  {dashboardLayouts.map((layout) => (
                    <button
                      key={layout.id}
                      className={`layout-switcher__chip ${
                        dashboardLayout === layout.id ? "layout-switcher__chip--active" : ""
                      }`}
                      onClick={() => setDashboardLayout(layout.id)}
                      title={layout.description}
                    >
                      {layout.name}
                    </button>
                  ))}
                </div>
                <button
                  className="button ghost"
                  onClick={refreshSession}
                  disabled={loading || !selectedId}
                >
                  Refresh
                </button>
                <button
                  className="button"
                  onClick={buildReview}
                  disabled={buildStatus === "loading" || !selectedId}
                >
                  {changeTree.length === 0 ? "Build Review" : "Rebuild Review"}
                </button>
              </div>
            </header>
          {error && <div className="alert">{error}</div>}
          <main
            className={`app__content dashboard-content ${
              chatOpen ? "app__content--chat-open" : ""
            } ${chatOpen && chatExpanded ? "app__content--chat-expanded" : ""}`.trim()}
          >
            <section className="review">
              {!detail && (
                <div className="card">
                  Open the Yotei link from the PR comment to load this review session.
                </div>
              )}
              {detail && (
                <>
                  <div className="dashboard-kpis">
                    {sessionKpis.map((metric) => (
                      <article key={metric.label} className="dashboard-kpi">
                        <span className="dashboard-kpi__label">{metric.label}</span>
                        <strong className="dashboard-kpi__value">{metric.value}</strong>
                        <span className="dashboard-kpi__meta">{metric.meta}</span>
                      </article>
                    ))}
                  </div>
                  <div className="card review__header">
                    <div>
                      <div className="detail__meta">
                        <div>
                          <strong>{detail.owner}</strong>/{detail.name} · PR #{detail.prNumber}
                        </div>
                        <div className="detail__sub">
                          {detail.title ?? "Untitled"} · {detail.source} · {detail.defaultBranch}
                        </div>
                      </div>
                      <div className="detail__meta">
                        <div>
                          <span className="label">Base</span> {detail.baseSha}
                        </div>
                        <div>
                          <span className="label">Head</span> {detail.headSha}
                        </div>
                      </div>
                    </div>
                    <div className="review__header-actions">
                      <span className="badge">{fileChanges.length} files</span>
                      <button
                        className="button ghost danger"
                        onClick={handleDeleteSnapshot}
                        disabled={loading}
                      >
                        Delete Session
                      </button>
                    </div>
                  </div>
                  <div className="review__layout">
                    <div className="review__column review__column--center">
                      <div className="card tabs-card">
                        <div className="tabs">
                          {[
                            { id: "summary", label: "Summary" },
                            { id: "flow", label: "Flow" },
                            { id: "tree", label: "Tree" },
                            { id: "files", label: "Files" },
                            { id: "diff", label: "Focused Diff" },
                            { id: "fullDiff", label: "Full PR Diff" }
                          ].map((tab) => (
                            <button
                              key={tab.id}
                              className={`tabs__button ${centerTab === tab.id ? "tabs__button--active" : ""}`}
                              onClick={() => setCenterTab(tab.id)}
                            >
                              {tab.label}
                            </button>
                          ))}
                        </div>
                      </div>
                  {centerTab === "summary" && (
                    <div className="card">
                      <div className="summary__header">
                        <h3>Change Summary</h3>
                        <span className="badge">{summary?.changedFilesCount ?? 0} files</span>
                      </div>
                      {summaryStatus === "ready" && summary && (
                        <div className="summary__overview">
                          <div className="summary__overview-title">Overall summary</div>
                          <p className="summary__overview-text">
                            {summary.overallSummary?.trim() || "Summary will appear after the AI pass completes."}
                          </p>
                          <div className="summary__overview-grid">
                            <div className="summary__overview-item">
                              <div className="summary__label">Before</div>
                              <p className="summary__overview-text">
                                {summary.beforeState?.trim() || "Baseline behavior not described yet."}
                              </p>
                            </div>
                            <div className="summary__overview-item">
                              <div className="summary__label">Now</div>
                              <p className="summary__overview-text">
                                {summary.afterState?.trim() || "Change impact not described yet."}
                              </p>
                            </div>
                          </div>
                        </div>
                      )}
                      {summaryError && <p className="alert">{summaryError}</p>}
                      {summaryStatus === "loading" && <p>Loading summary...</p>}
                      {summaryStatus === "empty" && (
                        <p>Build the review to generate the change summary.</p>
                      )}
                      {summaryStatus === "ready" && summary && (
                        <div className="summary__grid">
                          <div className="summary__item">
                            <div className="summary__label">Files changed</div>
                            <div className="summary__value">{summary.changedFilesCount}</div>
                            <div className="summary__sub">
                              {summary.newFilesCount} new · {summary.modifiedFilesCount} modified ·{" "}
                              {summary.deletedFilesCount} deleted
                            </div>
                          </div>
                          <div className="summary__item">
                            <div className="summary__label">Entry points</div>
                            <div className="summary__list">
                              {(summary.entryPoints ?? []).length === 0 && (
                                <span className="summary__empty">None detected</span>
                              )}
                              {(summary.entryPoints ?? []).map((item) => (
                                <span key={item} className="chip">
                                  {item}
                                </span>
                              ))}
                            </div>
                          </div>
                          <div className="summary__item">
                            <div className="summary__label">Side effects</div>
                            <div className="summary__list">
                              {(summary.sideEffects ?? []).length === 0 && (
                                <span className="summary__empty">None detected</span>
                              )}
                              {(summary.sideEffects ?? []).map((item) => (
                                <span key={item} className="chip">
                                  {item}
                                </span>
                              ))}
                            </div>
                          </div>
                          <div className="summary__item">
                            <div className="summary__label">Risk tags</div>
                            <div className="summary__list">
                              {(summary.riskTags ?? []).length === 0 && (
                                <span className="summary__empty">None detected</span>
                              )}
                              {(summary.riskTags ?? []).map((item) => (
                                <span key={item} className="chip">
                                  {item}
                                </span>
                              ))}
                            </div>
                          </div>
                        </div>
                      )}
                    </div>
                  )}
                  {centerTab === "flow" && (
                    <div className="card flow">
                      <div className="flow__header">
                        <h3>Execution Flow</h3>
                        <span className="badge">{flowGraph.nodes.length} nodes</span>
                      </div>
                      {flowError && <p className="alert">{flowError}</p>}
                      {flowStatus === "loading" && <p>Loading flow graph...</p>}
                      {flowStatus === "empty" && <p>Build the review to generate a flow.</p>}
                      {flowStatus === "ready" && flowGraph.nodes.length === 0 && (
                        <p>No flow nodes available yet.</p>
                      )}
                      {flowGraph.nodes.length > 0 && (
                        <div className="flow__canvas">
                          <ReactFlow
                            nodes={flowLayout.nodes}
                            edges={flowLayout.edges}
                            onNodeClick={handleFlowNodeClick}
                            fitView
                            fitViewOptions={{ padding: 0.2 }}
                            nodesDraggable={false}
                            nodesConnectable={false}
                            elementsSelectable
                          >
                            <Background gap={20} color="rgba(255,255,255,0.08)" />
                            <Controls position="bottom-right" />
                          </ReactFlow>
                        </div>
                      )}
                    </div>
                  )}
                  {centerTab === "tree" && (
                    <div className="card">
                      <div className="tree__header">
                        <h3>Review Tree</h3>
                        <div className="tree__actions">
                          <span className="badge">{changeTree.length} nodes</span>
                          <button
                            className="button ghost"
                            onClick={buildReview}
                            disabled={buildStatus === "loading" || !selectedId}
                          >
                            {changeTree.length === 0 ? "Build Review" : "Rebuild Review"}
                          </button>
                        </div>
                      </div>
                      {treeError && <p className="alert">{treeError}</p>}
                      {buildError && <p className="alert">{buildError}</p>}
                      {buildStatus === "loading" && <p>Building change tree...</p>}
                      {changeTree.length === 0 && <p>No change tree yet.</p>}
                      {changeTree.length > 0 && <div className="tree__list">{renderTree()}</div>}
                    </div>
                  )}
                  {centerTab === "files" && (
                    <div className="card">
                      <div className="detail__filters">
                        <label className="field">
                          Change type
                          <input
                            value={changeTypeFilter}
                            onChange={(event) => setChangeTypeFilter(event.target.value)}
                            placeholder="added / modified / deleted"
                          />
                        </label>
                        <label className="field">
                          Path prefix
                          <input
                            value={pathPrefixFilter}
                            onChange={(event) => setPathPrefixFilter(event.target.value)}
                            placeholder="src/api/"
                          />
                        </label>
                      </div>
                      <div className="detail__list">
                        {fileChanges.length === 0 && <p>No file changes yet.</p>}
                        {fileChanges.map((change) => (
                          <button
                            key={change.path}
                            className={`detail__item ${
                              selectedChange?.path === change.path ? "detail__item--active" : ""
                            }`}
                            onClick={() => {
                              setSelectedChange(change);
                              setSelectedNode(null);
                              setSelectedFlowNodeId(null);
                              setBehaviourSummary(null);
                              setBehaviourStatus("idle");
                              setChecklist([]);
                              setChecklistStatus("idle");
                              setChecklistDraft("");
                              setChecklistAddStatus("idle");
                              setChecklistAddError("");
                              setReviewerQuestions([]);
                              setQuestionsStatus("idle");
                              setQuestionsError("");
                              setQuestionsSource("");
                            }}
                          >
                            <div className="detail__path">{change.path}</div>
                            <div className="detail__stats">
                              <span className="pill">{change.changeType}</span>
                              <span className="pill">+{change.addedLines}</span>
                              <span className="pill">-{change.deletedLines}</span>
                            </div>
                            {change.rawDiffRef && <div className="detail__ref">{change.rawDiffRef}</div>}
                          </button>
                        ))}
                      </div>
                    </div>
                  )}
                  {centerTab === "diff" && (
                    <div className="card diff">
                      <div className="diff__header">
                        <h3>Raw Diff</h3>
                        <div className="diff__summary">
                          <span className="badge">{selectedNodePath ?? selectedChange?.path ?? "None"}</span>
                          <div className="diff__stats">
                            <span className="diff__badge diff__badge--add">+{diffStats.added}</span>
                            <span className="diff__badge diff__badge--del">-{diffStats.removed}</span>
                          </div>
                        </div>
                      </div>
                      {diffStatus === "idle" && <p>Select a file change to view raw diff.</p>}
                      {diffStatus === "loading" && <p>Loading diff...</p>}
                      {diffStatus === "error" && <p className="diff__error">{diffText}</p>}
                      {diffStatus === "ready" && (
                        <div className="diff__viewer">
                          {diffLines.length === 0 && <p className="diff__text">No diff content.</p>}
                          {diffLines.map((line, index) => (
                            <div key={`${line.type}-${index}`} className={`diff__row diff__row--${line.type}`}>
                              <div className="diff__gutter">
                                <span className="diff__line-number">{line.oldNumber}</span>
                                <span className="diff__line-number">{line.newNumber}</span>
                              </div>
                              <div className="diff__line-content">{line.text}</div>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                  {centerTab === "fullDiff" && (
                    <div className="card full-diff">
                      <div className="full-diff__header">
                        <div>
                          <h3>Full PR Diff</h3>
                          <p className="diff__text">Browse file changes without leaving Yotei.</p>
                        </div>
                        <div className="full-diff__controls">
                          <button
                            className={`button ghost ${fullDiffMode === "single" ? "button--active" : ""}`}
                            onClick={() => setFullDiffMode("single")}
                          >
                            File View
                          </button>
                          <button
                            className={`button ghost ${fullDiffMode === "all" ? "button--active" : ""}`}
                            onClick={() => setFullDiffMode("all")}
                          >
                            Full Patch
                          </button>
                        </div>
                      </div>
                      <div className="full-diff__layout">
                        <div className="full-diff__sidebar">
                          <label className="field">
                            Filter files
                            <input
                              value={fullDiffFilter}
                              onChange={(event) => setFullDiffFilter(event.target.value)}
                              placeholder="Search by path"
                            />
                          </label>
                          <div className="full-diff__files">
                            {fileChanges.length === 0 && <p>No file changes yet.</p>}
                            {fileChanges
                              .filter((change) =>
                                fullDiffFilter
                                  ? change.path.toLowerCase().includes(fullDiffFilter.toLowerCase())
                                  : true
                              )
                              .map((change) => (
                                <button
                                  key={change.path}
                                  className={`full-diff__file ${
                                    fullDiffActivePath === change.path ? "full-diff__file--active" : ""
                                  }`}
                                  onClick={() => {
                                    setFullDiffMode("single");
                                    setFullDiffActivePath(change.path);
                                  }}
                                >
                                  <div className="full-diff__file-main">
                                    <span className={`full-diff__status full-diff__status--${change.changeType}`}>
                                      {change.changeType.slice(0, 1).toUpperCase()}
                                    </span>
                                    <span className="full-diff__file-path">{change.path}</span>
                                  </div>
                                  <div className="full-diff__file-stats">
                                    <span className="diff__badge diff__badge--add">+{change.addedLines}</span>
                                    <span className="diff__badge diff__badge--del">-{change.deletedLines}</span>
                                  </div>
                                </button>
                              ))}
                          </div>
                        </div>
                        <div className="full-diff__content">
                          <div className="diff__header">
                            <h4>
                              {fullDiffMode === "all"
                                ? "All files"
                                : fullDiffActivePath ?? "Select a file"}
                            </h4>
                            <div className="diff__summary">
                              <div className="diff__stats">
                                <span className="diff__badge diff__badge--add">+{fullDiffStats.added}</span>
                                <span className="diff__badge diff__badge--del">-{fullDiffStats.removed}</span>
                              </div>
                            </div>
                          </div>
                          {fullDiffStatus === "loading" && <p>Loading full diff...</p>}
                          {fullDiffStatus === "error" && <p className="diff__error">{fullDiffError}</p>}
                          {fullDiffStatus !== "loading" && (
                            <div className="diff__viewer diff__viewer--full">
                              {fullDiffLines.length === 0 && (
                                <p className="diff__text">Select a file to view its patch.</p>
                              )}
                              {fullDiffLines.map((line, index) => (
                                <div key={`${line.type}-${index}`} className={`diff__row diff__row--${line.type}`}>
                                  <div className="diff__gutter">
                                    <span className="diff__line-number">{line.oldNumber}</span>
                                    <span className="diff__line-number">{line.newNumber}</span>
                                  </div>
                                  <div className="diff__line-content">{line.text}</div>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
                <div className="review__column review__column--right">
                  <div className="card tabs-card">
                    <div className="tabs">
                      {[
                        { id: "review", label: "Review" },
                        { id: "compliance", label: "Compliance" }
                      ].map((tab) => (
                        <button
                          key={tab.id}
                          className={`tabs__button ${rightTab === tab.id ? "tabs__button--active" : ""}`}
                          onClick={() => setRightTab(tab.id)}
                        >
                          {tab.label}
                        </button>
                      ))}
                    </div>
                    {rightTab === "review" && (
                      <div className="tabs__panel">
                        <div className="diff__header">
                          <h3>Behaviour Summary</h3>
                          <span className="badge">{selectedNode ? selectedNode.label : "None"}</span>
                        </div>
                        {behaviourStatus === "idle" && <p>Select a file node to view behaviour.</p>}
                        {behaviourStatus === "loading" && <p>Loading behaviour summary...</p>}
                        {behaviourStatus === "error" && <p className="diff__error">{behaviourError}</p>}
                        {behaviourStatus === "empty" && (
                          <p className="diff__text">Build the review to generate behaviour summaries.</p>
                        )}
                        {behaviourStatus === "ready" && behaviourSummary && (
                          <ul className="diff__list">
                            <li>{behaviourSummary.behaviourChange}</li>
                            <li>{behaviourSummary.scope}</li>
                            <li>{behaviourSummary.reviewerFocus}</li>
                          </ul>
                        )}
                        <div className="diff__spacer" />
                        <div className="diff__header">
                          <h3>Review Checklist</h3>
                        </div>
                        {checklistStatus === "idle" && <p>Select a file node to view checklist.</p>}
                        {checklistStatus === "loading" && <p>Loading checklist...</p>}
                        {checklistStatus === "error" && <p className="diff__error">{checklistError}</p>}
                        {checklistStatus === "empty" && (
                          <p className="diff__text">Build the review to generate a checklist.</p>
                        )}
                        {checklistStatus === "ready" && (
                          <div className="checklist__groups">
                            {checklistGroups.map((group) => (
                              <div key={group.source} className="checklist__group">
                                <div className="checklist__header">
                                  <span className="pill">{group.source}</span>
                                  {group.source === "conversation" && (
                                    <span className="badge">New from conversation</span>
                                  )}
                                </div>
                                <ul className="diff__list">
                                  {group.items.map((item) => (
                                    <li key={`${group.source}-${item.text}`}>{item.text}</li>
                                  ))}
                                </ul>
                              </div>
                            ))}
                          </div>
                        )}
                        <div className="checklist__add">
                          <label className="field field--full">
                            Add from conversation
                            <input
                              value={checklistDraft}
                              onChange={(event) => setChecklistDraft(event.target.value)}
                              placeholder="New checklist item from conversation"
                            />
                          </label>
                          {checklistAddError && <p className="diff__error">{checklistAddError}</p>}
                          <div className="checklist__actions">
                            <button
                              className="button"
                              onClick={() => addChecklistItem(selectedNode?.id, checklistDraft)}
                              disabled={
                                checklistAddStatus === "saving" ||
                                !selectedNode ||
                                selectedNode.nodeType !== "file"
                              }
                            >
                              Add Item
                            </button>
                            <button
                              className="button ghost"
                              onClick={() => setChecklistDraft("")}
                              disabled={!checklistDraft}
                            >
                              Clear
                            </button>
                          </div>
                        </div>
                        <div className="diff__spacer" />
                        <div className="diff__header">
                          <h3>Reviewer Questions</h3>
                          {questionsSource && <span className="badge">{questionsSource}</span>}
                        </div>
                        {questionsStatus === "idle" && (
                          <p>Select a file node to view reviewer questions.</p>
                        )}
                        {questionsStatus === "loading" && <p>Loading reviewer questions...</p>}
                        {questionsStatus === "error" && <p className="diff__error">{questionsError}</p>}
                        {questionsStatus === "empty" && (
                          <p className="diff__text">Build the review to generate reviewer questions.</p>
                        )}
                        {questionsStatus === "ready" && (
                          <ul className="diff__list">
                            {reviewerQuestions.map((item) => (
                              <li key={item}>{item}</li>
                            ))}
                          </ul>
                        )}
                        {selectedNode?.evidence?.length > 0 && (
                          <div className="diff__evidence">
                            <div className="diff__label">Evidence</div>
                            <div className="diff__chips">
                              {selectedNode.evidence.map((item) => (
                                <span key={item} className="chip">
                                  {item}
                                </span>
                              ))}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                    {rightTab === "compliance" && (
                      <div className="tabs__panel compliance">
                        <div className="diff__header">
                          <h3>Compliance Report</h3>
                          <span className="badge">{complianceReport ? "Ready" : "Draft"}</span>
                        </div>
                        <div className="compliance__actions">
                          <button
                            className="button"
                            onClick={() => fetchComplianceReport(selectedId)}
                            disabled={!selectedId || complianceStatus === "loading"}
                          >
                            Generate
                          </button>
                          <button
                            className="button ghost"
                            onClick={downloadComplianceReport}
                            disabled={!selectedId || complianceStatus === "loading"}
                          >
                            Download JSON
                          </button>
                        </div>
                        {complianceStatus === "idle" && (
                          <p className="diff__text">
                            Generate a compliance report to review risk and transcript evidence.
                          </p>
                        )}
                        {complianceStatus === "loading" && <p>Building compliance report...</p>}
                        {complianceStatus === "error" && (
                          <p className="diff__error">{complianceError}</p>
                        )}
                        {complianceStatus === "ready" && complianceReport && (
                          <div className="compliance__body">
                            <div className="compliance__section">
                              <div className="summary__label">Summary</div>
                              <div className="compliance__stats">
                                <span className="pill">
                                  {complianceReport.summary.changedFilesCount} files
                                </span>
                                <span className="pill">
                                  {complianceReport.summary.newFilesCount} new
                                </span>
                                <span className="pill">
                                  {complianceReport.summary.modifiedFilesCount} modified
                                </span>
                                <span className="pill">
                                  {complianceReport.summary.deletedFilesCount} deleted
                                </span>
                              </div>
                              <div className="summary__list">
                                {(complianceReport.summary.entryPoints ?? []).map((item) => (
                                  <span key={item} className="chip">
                                    {item}
                                  </span>
                                ))}
                                {(complianceReport.summary.sideEffects ?? []).map((item) => (
                                  <span key={item} className="chip">
                                    {item}
                                  </span>
                                ))}
                              </div>
                            </div>
                            <div className="compliance__section">
                              <div className="summary__label">Risk tags</div>
                              <div className="summary__list">
                                {(complianceReport.riskTags ?? []).length === 0 && (
                                  <span className="summary__empty">None detected</span>
                                )}
                                {(complianceReport.riskTags ?? []).map((item) => (
                                  <span key={item} className="chip">
                                    {item}
                                  </span>
                                ))}
                              </div>
                            </div>
                            <div className="compliance__section">
                              <div className="summary__label">Checklist coverage</div>
                              <div className="compliance__stats">
                                <span className="pill">
                                  {complianceReport.checklist.totalItems} items
                                </span>
                                <span className="pill">
                                  {complianceReport.checklist.conversationItems} conversation
                                </span>
                                <span className="pill">
                                  {complianceReport.checklist.fileNodeCount} files
                                </span>
                              </div>
                              {(complianceReport.checklist.items ?? []).length > 0 && (
                                <ul className="diff__list">
                                  {complianceReport.checklist.items.slice(0, 5).map((item) => (
                                    <li key={`${item.source}-${item.text}`}>{item.text}</li>
                                  ))}
                                </ul>
                              )}
                            </div>
                            <div className="compliance__section">
                              <div className="summary__label">Transcript highlights</div>
                              <div className="compliance__stats">
                                <span className="pill">
                                  {complianceReport.transcript.totalEntries} entries
                                </span>
                                {complianceReport.transcript.lastEntryAt && (
                                  <span className="pill">
                                    Last{" "}
                                    {new Date(
                                      complianceReport.transcript.lastEntryAt
                                    ).toLocaleDateString()}
                                  </span>
                                )}
                              </div>
                              {(complianceReport.transcriptHighlights ?? []).length > 0 && (
                                <ul className="voice__list">
                                  {complianceReport.transcriptHighlights.map((item) => (
                                    <li key={item.transcriptId} className="voice__entry">
                                      <div className="voice__entry-question">{item.question}</div>
                                      <div className="voice__entry-answer">{item.answer}</div>
                                    </li>
                                  ))}
                                </ul>
                              )}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </>
          )}
        </section>
        {chatOpen && (
          <aside className="chat-sidebar">
            <div className="voice-mascot__panel" id="voice-mascot-panel" role="dialog">
              <div className="voice-mascot__header">
                <div className="voice-mascot__title">
                  <h3 className="voice-mascot__title-label">New conversation</h3>
                  <span className="voice-mascot__caret" aria-hidden="true">
                    <svg viewBox="0 0 20 20" role="img">
                      <path
                        d="M5 7l5 6 5-6"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.6"
                      />
                    </svg>
                  </span>
                </div>
                <div className="voice-mascot__header-actions">
                  <button
                    className={`voice-mascot__icon-button ${
                      chatSoundMuted ? "voice-mascot__icon-button--active" : ""
                    }`}
                    type="button"
                    onClick={() => setChatSoundMuted((current) => !current)}
                    aria-pressed={chatSoundMuted}
                    aria-label="Toggle sound"
                  >
                    {chatSoundMuted ? (
                      <svg viewBox="0 0 24 24" className="voice-mascot__icon" aria-hidden="true">
                        <path
                          d="M4 9h4l5-4v14l-5-4H4z"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="1.6"
                        />
                        <path
                          d="M19 9l-4 4m0-4l4 4"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="1.6"
                          strokeLinecap="round"
                        />
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" className="voice-mascot__icon" aria-hidden="true">
                        <path
                          d="M4 9h4l5-4v14l-5-4H4z"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="1.6"
                        />
                        <path
                          d="M16 9.5a4 4 0 010 5"
                          fill="none"
                          stroke="currentColor"
                          strokeWidth="1.6"
                          strokeLinecap="round"
                        />
                      </svg>
                    )}
                  </button>
                  <button
                    className={`voice-mascot__icon-button ${
                      chatExpanded ? "voice-mascot__icon-button--active" : ""
                    }`}
                    type="button"
                    onClick={() => setChatExpanded((current) => !current)}
                    aria-pressed={chatExpanded}
                    aria-label="Toggle expand"
                  >
                    <svg viewBox="0 0 24 24" className="voice-mascot__icon" aria-hidden="true">
                      <path
                        d="M4 9V4h5M20 15v5h-5M15 4h5v5M9 20H4v-5"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.6"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      />
                    </svg>
                  </button>
                  <button
                    className="voice-mascot__icon-button"
                    type="button"
                    onClick={() => setChatOpen(false)}
                    aria-label="Close chat"
                  >
                    <svg viewBox="0 0 20 20" className="voice-mascot__icon" aria-hidden="true">
                      <path
                        d="M5 5l10 10M15 5l-10 10"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.6"
                        strokeLinecap="round"
                      />
                    </svg>
                  </button>
                </div>
              </div>
              <div className="voice-mascot__context">
                <div className="voice-mascot__focus">
                  <span className="voice-mascot__label">Focus</span>
                  <span className="badge">
                    {selectedNode ? selectedNode.label : "Pick a review node"}
                  </span>
                </div>
                <div className="voice-mascot__status">
                  <span
                    className={`voice__indicator ${
                      chatStatus === "sending" ? "voice__indicator--active" : ""
                    }`}
                  />
                  <span>{chatStatusLabel}</span>
                </div>
              </div>
              <div className="voice-mascot__thread">
                {transcriptStatus === "loading" && <p>Loading transcript...</p>}
                {transcriptStatus === "error" && <p className="diff__error">{transcriptError}</p>}
                {transcriptStatus === "ready" && transcriptEntries.length === 0 && (
                  <div className="chat-empty">
                    <img className="chat-empty__icon" src={LogoStandard} alt="Assistant" />
                    <p className="chat-empty__title">How can I help?</p>
                  </div>
                )}
                {transcriptEntries.length > 0 && (
                  <ul className="voice-thread">
                    {transcriptDisplay.map((entry) => (
                      <li key={entry.id} className="voice-thread__entry">
                        <div className="voice-thread__meta">
                          <span className="pill">{entry.nodeLabel}</span>
                          <span className="pill">{entry.createdAt}</span>
                        </div>
                        <div className="voice-thread__bubble voice-thread__bubble--question">
                          {entry.question}
                        </div>
                        <div className="voice-thread__bubble voice-thread__bubble--answer">
                          {entry.answer}
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              <form
                className="voice-mascot__composer"
                onSubmit={(event) => {
                  event.preventDefault();
                  sendChatMessage();
                }}
              >
                <label className="field field--full">
                  Your message
                  <textarea
                    value={chatDraft}
                    onChange={(event) => setChatDraft(event.target.value)}
                    placeholder="Ask a question…"
                    rows={3}
                  />
                </label>
                {chatError && <p className="alert">{chatError}</p>}
                <div className="voice-mascot__actions">
                  <button
                    className="button"
                    type="submit"
                    disabled={chatStatus === "sending" || !selectedNode || !selectedId}
                  >
                    Send Message
                  </button>
                  <button
                    className="button ghost"
                    type="button"
                    onClick={() => setChatDraft("")}
                    disabled={!chatDraft}
                  >
                    Clear
                  </button>
                </div>
              </form>
            </div>
          </aside>
        )}
      </main>
      <footer className="app__footer">
        <div className="status">
          <span className="status__label">Active session</span>
          <span className="status__value">{activeSnapshotName || "None selected"}</span>
        </div>
      </footer>
          <div className={`voice-mascot ${chatOpen ? "voice-mascot--hidden" : ""}`}>
            <button
              className="voice-mascot__button"
              type="button"
              onClick={() => setChatOpen((current) => !current)}
              aria-expanded={chatOpen}
              aria-controls="voice-mascot-panel"
            >
              <span className="voice-mascot__glow" aria-hidden="true" />
              <img className="voice-mascot__icon" src={LogoStandard} alt="Open chat" />
            </button>
            <div className="voice-mascot__hint">Ask about this PR</div>
          </div>
          </div>
        </div>
      )}
    </div>
  );
}

import { useEffect, useMemo, useRef, useState } from "react";
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

export default function App() {
  const [snapshots, setSnapshots] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
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
  const [voiceQuery, setVoiceQuery] = useState("");
  const [voiceStatus, setVoiceStatus] = useState("idle");
  const [voiceError, setVoiceError] = useState("");
  const [recordingSupported, setRecordingSupported] = useState(false);
  const [mascotOpen, setMascotOpen] = useState(false);
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
  const limit = 10;
  const [offset, setOffset] = useState(0);
  const [changeTypeFilter, setChangeTypeFilter] = useState("");
  const [pathPrefixFilter, setPathPrefixFilter] = useState("");
  const [sessionsCollapsed, setSessionsCollapsed] = useState(false);
  const recognitionRef = useRef(null);

  const fetchSnapshots = async (nextOffset = offset, nextLimit = limit) => {
    setLoading(true);
    setError("");
    try {
      const res = await fetch(
        `${apiBase}/review-sessions?limit=${nextLimit}&offset=${nextOffset}`
      );
      if (!res.ok) {
        throw new Error("Failed to load review sessions");
      }
      const data = await res.json();
      setSnapshots(data);
      if (!selectedId && data.length > 0) {
        setSelectedId(data[0].id);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const fetchDetail = async (sessionId) => {
    if (!sessionId) {
      setDetail(null);
      return;
    }
    setLoading(true);
    setError("");
    try {
      const res = await fetch(`${apiBase}/review-sessions/${sessionId}`);
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
      const res = await fetch(
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
      const res = await fetch(`${apiBase}/review-sessions/${snapshotId}/change-tree`);
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
      const res = await fetch(`${apiBase}/review-sessions/${sessionId}/summary`);
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
      const res = await fetch(`${apiBase}/review-sessions/${sessionId}/flow`);
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
      const res = await fetch(`${apiBase}/review-sessions/${sessionId}/transcript`);
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
      const res = await fetch(
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
      const res = await fetch(`${apiBase}/review-sessions/${sessionId}/compliance-report`);
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
      const res = await fetch(`${apiBase}/insights/org${query ? `?${query}` : ""}`);
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
      const res = await fetch(`${apiBase}/review-sessions/${selectedId}/build`, {
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
      const res = await fetch(`${apiBase}/review-nodes/${nodeId}/behaviour-summary`);
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
      const res = await fetch(`${apiBase}/review-nodes/${nodeId}/checklist`);
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
      const res = await fetch(`${apiBase}/review-nodes/${nodeId}/questions`);
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
      const res = await fetch(`${apiBase}/review-nodes/${nodeId}/checklist/items`, {
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

  const sendVoiceQuery = async () => {
    if (!selectedNode) {
      setVoiceError("Select a review node before sending a voice query.");
      return;
    }
    const questionText = voiceQuery.trim();
    if (!questionText) {
      setVoiceError("Provide a question or transcript first.");
      return;
    }
    setVoiceStatus("sending");
    setVoiceError("");
    try {
      const res = await fetch(`${apiBase}/review-nodes/${selectedNode.id}/voice-query`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ question: questionText })
      });
      if (!res.ok) {
        throw new Error("Failed to send voice query");
      }
      await res.json();
      setVoiceQuery("");
      await addChecklistItem(selectedNode.id, questionText, "conversation");
      await fetchTranscript(selectedId);
      setVoiceStatus("idle");
    } catch (err) {
      setVoiceError(err.message);
      setVoiceStatus("error");
    }
  };

  const startRecording = () => {
    if (!recordingSupported || !recognitionRef.current) {
      setVoiceError("Speech recognition is not supported in this browser.");
      return;
    }
    setMascotOpen(true);
    setVoiceError("");
    setVoiceStatus("recording");
    recognitionRef.current.start();
  };

  const stopRecording = () => {
    if (recognitionRef.current) {
      recognitionRef.current.stop();
    }
    if (voiceStatus === "recording") {
      setVoiceStatus("idle");
    }
  };

  const fetchDiff = async (snapshotId, path) => {
    if (!snapshotId || !path) {
      return;
    }
    setDiffStatus("loading");
    setDiffText("");
    try {
      const res = await fetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
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
      const res = await fetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
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
          const res = await fetch(`${apiBase}/raw-diffs/${snapshotId}?path=${encodeURIComponent(path)}`);
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
      const res = await fetch(`${apiBase}/review-nodes/${nodeId}/diff`);
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
      const res = await fetch(`${apiBase}/snapshots/${selectedId}`, { method: "DELETE" });
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
      setVoiceQuery("");
      setVoiceStatus("idle");
      setVoiceError("");
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
      await fetchSnapshots(0, limit);
      setOffset(0);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
      setRecordingSupported(false);
      return;
    }

    setRecordingSupported(true);
    const recognition = new SpeechRecognition();
    recognition.lang = "en-US";
    recognition.interimResults = false;
    recognition.continuous = false;

    recognition.onresult = (event) => {
      const transcript = Array.from(event.results)
        .map((result) => result[0]?.transcript ?? "")
        .join(" ")
        .trim();
      if (transcript) {
        setVoiceQuery(transcript);
      }
      setVoiceStatus("idle");
    };

    recognition.onerror = (event) => {
      setVoiceError(event.error ?? "Speech recognition error");
      setVoiceStatus("error");
    };

    recognition.onend = () => {
      setVoiceStatus("idle");
    };

    recognitionRef.current = recognition;

    return () => {
      recognition.stop();
    };
  }, []);

  useEffect(() => {
    fetchSnapshots();
  }, []);

  const activeSnapshotName = useMemo(() => {
    if (!detail) return "";
    return `${detail.owner}/${detail.name} · PR #${detail.prNumber}`;
  }, [detail]);

  const selectedRepoFilter = useMemo(() => {
    if (!detail?.owner || !detail?.name) {
      return null;
    }
    return `${detail.owner}/${detail.name}`;
  }, [detail]);

  useEffect(() => {
    if (insightsScope === "repo" && !selectedRepoFilter) {
      setOrgInsights(null);
      setInsightsStatus("idle");
      setInsightsError("");
      return;
    }
    fetchOrgInsights(insightsScope === "repo" ? selectedRepoFilter : null);
  }, [insightsScope, selectedRepoFilter]);

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
      setVoiceQuery("");
      setVoiceStatus("idle");
      setVoiceError("");
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
    fetchSnapshots(offset, limit);
  }, [limit, offset]);

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

  const handlePrev = () => {
    setOffset((current) => Math.max(0, current - limit));
  };

  const handleNext = () => {
    setOffset((current) => current + limit);
  };

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

  const transcriptPreview = useMemo(() => {
    return transcriptDisplay.slice(-6).reverse();
  }, [transcriptDisplay]);

  const voiceStatusLabel = useMemo(() => {
    if (voiceStatus === "recording") {
      return "Listening…";
    }
    if (voiceStatus === "sending") {
      return "Sending your question…";
    }
    if (voiceStatus === "error") {
      return "Needs attention";
    }
    if (!recordingSupported) {
      return "Speech recognition unavailable";
    }
    return "Ready to listen";
  }, [recordingSupported, voiceStatus]);

  const mascotActive = mascotOpen || voiceStatus === "recording" || voiceStatus === "sending";

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

  return (
    <div className="app">
      <header className="app__header">
        <div>
          <h1>Yotei</h1>
          <p>Review comprehension for AI-generated changes.</p>
        </div>
        <div className="app__actions">
          <button className="button ghost" onClick={() => fetchSnapshots()} disabled={loading}>
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
      <main className={`app__content ${sessionsCollapsed ? "app__content--collapsed" : ""}`}>
        <aside className={`sidebar ${sessionsCollapsed ? "sidebar--collapsed" : ""}`}>
          <section className={`card sessions ${sessionsCollapsed ? "card--collapsed" : ""}`}>
            <div className="card__header">
              <h2>Review Sessions</h2>
              <div className="card__actions">
                <button
                  className="button ghost icon-button"
                  onClick={() => setSessionsCollapsed((current) => !current)}
                  aria-label={sessionsCollapsed ? "Expand review sessions" : "Collapse review sessions"}
                >
                  {sessionsCollapsed ? (
                    <svg viewBox="0 0 20 20" aria-hidden="true">
                      <path
                        d="M7 4l6 6-6 6"
                        fill="none"
                        stroke="currentColor"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2"
                      />
                    </svg>
                  ) : (
                    <svg viewBox="0 0 20 20" aria-hidden="true">
                      <path
                        d="M4 7l6 6 6-6"
                        fill="none"
                        stroke="currentColor"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeWidth="2"
                      />
                    </svg>
                  )}
                </button>
              </div>
            </div>
            {!sessionsCollapsed && (
              <>
                <div className="pagination">
                  <div className="pager">
                    <button className="button ghost" onClick={handlePrev} disabled={offset === 0}>
                      Prev
                    </button>
                    <button className="button ghost" onClick={handleNext} disabled={loading}>
                      Next
                    </button>
                  </div>
                </div>
                {snapshots.length === 0 && <p>No review sessions found.</p>}
                <ul className="list">
                  {snapshots.map((snapshot) => (
                    <li key={snapshot.id}>
                      <button
                        className={`list__item ${
                          snapshot.id === selectedId ? "list__item--active" : ""
                        }`}
                        onClick={() => setSelectedId(snapshot.id)}
                      >
                        <div className="list__title">
                          {snapshot.owner}/{snapshot.name} · PR #{snapshot.prNumber}
                        </div>
                        <div className="list__meta">
                          {snapshot.title ?? "Untitled"} · {snapshot.headSha}
                        </div>
                      </button>
                    </li>
                  ))}
                </ul>
              </>
            )}
          </section>
          {!sessionsCollapsed && (
            <section className="card insights">
              <div className="card__header">
                <h2>Org Insights</h2>
                <div className="card__actions insights__actions">
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
              </div>
              {insightsScope === "repo" && detail && (
                <div className="insights__meta">
                  <span className="pill">
                    {detail.owner}/{detail.name}
                  </span>
                </div>
              )}
              {insightsError && <p className="alert">{insightsError}</p>}
              {insightsStatus === "loading" && <p>Loading insights...</p>}
              {insightsStatus === "idle" && insightsScope === "repo" && !detail && (
                <p className="diff__text">Select a review session to scope insights to a repo.</p>
              )}
              {insightsStatus === "ready" && orgInsights && (
                <div className="insights__body">
                  {(orgInsights.from || orgInsights.to) && (
                    <div className="insights__meta">
                      {orgInsights.from && (
                        <span className="pill">
                          From {new Date(orgInsights.from).toLocaleDateString()}
                        </span>
                      )}
                      {orgInsights.to && (
                        <span className="pill">
                          To {new Date(orgInsights.to).toLocaleDateString()}
                        </span>
                      )}
                    </div>
                  )}
                  <div className="insights__stats">
                    <div className="insights__stat">
                      <div className="summary__label">Sessions</div>
                      <div className="summary__value">{orgInsights.reviewSessionCount}</div>
                      <div className="summary__sub">
                        {orgInsights.reviewSummaryCount} summaries
                      </div>
                    </div>
                    <div className="insights__stat">
                      <div className="summary__label">Repositories</div>
                      <div className="summary__value">{orgInsights.repositories.length}</div>
                      <div className="summary__sub">
                        {insightsScope === "repo" ? "Filtered" : "Active"}
                      </div>
                    </div>
                  </div>
                  <div className="insights__section">
                    <div className="insights__section-header">
                      <h3>Repositories</h3>
                      <span className="badge">{insightsData.repoItems.length}</span>
                    </div>
                    {insightsData.repoItems.length === 0 && (
                      <p className="diff__text">No repository activity yet.</p>
                    )}
                    {insightsData.repoItems.length > 0 && (
                      <div className="insights__list">
                        {insightsData.repoItems.slice(0, 5).map((item) => (
                          <div key={`${item.owner}/${item.name}`} className="insights__row">
                            <span className="insights__label">
                              {item.owner}/{item.name}
                            </span>
                            <div className="insights__bar">
                              <span
                                style={{
                                  width: `${(item.reviewSessionCount / insightsData.maxRepo) * 100}%`
                                }}
                              />
                            </div>
                            <span className="insights__count">{item.reviewSessionCount}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="insights__section">
                    <div className="insights__section-header">
                      <h3>Risk tags</h3>
                      <span className="badge">{insightsData.riskItems.length}</span>
                    </div>
                    {insightsData.riskItems.length === 0 && (
                      <p className="diff__text">No risk tags yet.</p>
                    )}
                    {insightsData.riskItems.length > 0 && (
                      <div className="insights__list">
                        {insightsData.riskItems.slice(0, 5).map((item) => (
                          <div key={item.label} className="insights__row">
                            <span className="insights__label">{item.label}</span>
                            <div className="insights__bar">
                              <span
                                style={{
                                  width: `${(item.count / insightsData.maxRisk) * 100}%`
                                }}
                              />
                            </div>
                            <span className="insights__count">{item.count}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="insights__section">
                    <div className="insights__section-header">
                      <h3>Hot paths</h3>
                      <span className="badge">{insightsData.hotItems.length}</span>
                    </div>
                    {insightsData.hotItems.length === 0 && (
                      <p className="diff__text">No hot paths yet.</p>
                    )}
                    {insightsData.hotItems.length > 0 && (
                      <div className="insights__list">
                        {insightsData.hotItems.slice(0, 5).map((item) => (
                          <div key={item.label} className="insights__row">
                            <span className="insights__label insights__label--mono">{item.label}</span>
                            <div className="insights__bar">
                              <span
                                style={{ width: `${(item.count / insightsData.maxHot) * 100}%` }}
                              />
                            </div>
                            <span className="insights__count">{item.count}</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="insights__section">
                    <div className="insights__section-header">
                      <h3>Review volume</h3>
                      <span className="badge">{insightsData.volumeItems.length}</span>
                    </div>
                    {insightsData.volumeItems.length === 0 && (
                      <p className="diff__text">No review volume yet.</p>
                    )}
                    {insightsData.volumeItems.length > 0 && (
                      <>
                        <div className="insights__note">Last 7 days</div>
                        <div className="insights__list">
                          {insightsData.volumeDisplay.map((item) => (
                            <div key={item.date} className="insights__row">
                              <span className="insights__label insights__label--mono">
                                {new Date(item.date).toLocaleDateString()}
                              </span>
                              <div className="insights__bar">
                                <span
                                  style={{
                                    width: `${(item.count / insightsData.maxVolume) * 100}%`
                                  }}
                                />
                              </div>
                              <span className="insights__count">{item.count}</span>
                            </div>
                          ))}
                        </div>
                      </>
                    )}
                  </div>
                </div>
              )}
            </section>
          )}
        </aside>
        <section className="review">
          {!detail && <div className="card">Select a review session to view details.</div>}
          {detail && (
            <>
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
      </main>
      <footer className="app__footer">
        <div className="status">
          <span className="status__label">Active session</span>
          <span className="status__value">{activeSnapshotName || "None selected"}</span>
        </div>
      </footer>
      <div
        className={`voice-mascot ${mascotOpen ? "voice-mascot--open" : ""} ${
          mascotActive ? "voice-mascot--active" : ""
        }`}
      >
        <div
          className="voice-mascot__panel"
          id="voice-mascot-panel"
          role="dialog"
          aria-modal="false"
          aria-hidden={!mascotOpen}
        >
          <div className="voice-mascot__header">
            <div>
              <p className="voice-mascot__eyebrow">PR Voice Companion</p>
              <h3>Ask about this PR</h3>
              <p className="voice-mascot__subtext">
                Talk through diffs, risk, and checklist gaps while you review.
              </p>
            </div>
            <button
              className="voice-mascot__close"
              type="button"
              onClick={() => setMascotOpen(false)}
              aria-label="Close voice assistant"
            >
              X
            </button>
          </div>
          <div className="voice-mascot__context">
            <div className="voice-mascot__focus">
              <span className="voice-mascot__label">Focus</span>
              <span className="badge">{selectedNode ? selectedNode.label : "Pick a review node"}</span>
            </div>
            <div className="voice-mascot__status">
              <span
                className={`voice__indicator ${
                  voiceStatus === "recording" || voiceStatus === "sending"
                    ? "voice__indicator--active"
                    : ""
                }`}
              />
              <span>{voiceStatusLabel}</span>
            </div>
          </div>
          <div className="voice-mascot__controls">
            <button
              className={`button voice-mascot__record ${
                voiceStatus === "recording" ? "voice-mascot__record--active" : ""
              }`}
              onClick={() => {
                if (voiceStatus === "recording") {
                  stopRecording();
                } else {
                  startRecording();
                }
              }}
              disabled={!recordingSupported || voiceStatus === "sending"}
            >
              {voiceStatus === "recording" ? "Stop Listening" : "Start Listening"}
            </button>
            <button
              className="button ghost"
              type="button"
              onClick={() => setVoiceQuery("")}
              disabled={!voiceQuery}
            >
              Clear
            </button>
          </div>
          <label className="field field--full">
            Your question
            <textarea
              value={voiceQuery}
              onChange={(event) => setVoiceQuery(event.target.value)}
              placeholder="Ask about specific files, risk, or reviewer focus…"
              rows={3}
            />
          </label>
          {voiceError && <p className="alert">{voiceError}</p>}
          <div className="voice-mascot__actions">
            <button
              className="button"
              onClick={sendVoiceQuery}
              disabled={voiceStatus === "sending" || !selectedNode}
            >
              Send Question
            </button>
            <p className="voice-mascot__note">
              {selectedNode
                ? "Responses land in the transcript and checklist."
                : "Select a review node to ground the response."}
            </p>
          </div>
          <div className="voice-mascot__thread">
            <div className="voice-mascot__thread-header">
              <div className="voice-mascot__thread-title">
                <h4>Conversation</h4>
                <span className="badge">{transcriptEntries.length}</span>
              </div>
              <div className="voice-mascot__thread-actions">
                <button
                  className="button ghost"
                  onClick={() => exportTranscript("json")}
                  disabled={!selectedId || transcriptExportStatus === "loading"}
                >
                  Export JSON
                </button>
                <button
                  className="button ghost"
                  onClick={() => exportTranscript("csv")}
                  disabled={!selectedId || transcriptExportStatus === "loading"}
                >
                  Export CSV
                </button>
              </div>
            </div>
            {transcriptStatus === "loading" && <p>Loading transcript...</p>}
            {transcriptStatus === "error" && <p className="diff__error">{transcriptError}</p>}
            {transcriptStatus === "ready" && transcriptEntries.length === 0 && (
              <p className="diff__text">No transcript entries yet.</p>
            )}
            {transcriptEntries.length > 0 && (
              <ul className="voice-thread">
                {transcriptPreview.map((entry) => (
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
        </div>
        <button
          className="voice-mascot__button"
          type="button"
          onClick={() => setMascotOpen((current) => !current)}
          aria-expanded={mascotOpen}
          aria-controls="voice-mascot-panel"
        >
          <span className="voice-mascot__glow" aria-hidden="true" />
          <img className="voice-mascot__icon" src={LogoStandard} alt="Voice mascot" />
        </button>
        <div className="voice-mascot__hint">Ask about this PR</div>
      </div>
    </div>
  );
}

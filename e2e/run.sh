#!/bin/sh
set -e

API_URL=${API_URL:-http://api:8080}
UI_URL=${UI_URL:-http://frontend:5173}

echo "Running API integration tests"
dotnet test /workspace/backend/tests/Api.IntegrationTests/Api.IntegrationTests.csproj

check() {
  name=$1
  url=$2
  echo "Checking ${name} at ${url}"
  for i in $(seq 1 30); do
    if curl -fsS "$url" >/dev/null; then
      echo "${name} is up"
      return 0
    fi
    sleep 1
  done
  echo "${name} failed to respond"
  return 1
}

check "api" "${API_URL}/health"
check "ui" "${UI_URL}"

timestamp=$(date +%s)
payload=$(cat <<JSON
{
  "owner": "e2e",
  "name": "fixtures",
  "prNumber": 1,
  "baseSha": "base-${timestamp}",
  "headSha": "head-${timestamp}",
  "defaultBranch": "main",
  "source": "fixture",
  "title": "E2E ingest"
}
JSON
)

echo "Posting snapshot ingest"
response=$(curl -fsS -X POST "${API_URL}/ingest/snapshot" -H "Content-Type: application/json" -d "${payload}")
echo "${response}" | grep -q "\"created\":true"
snapshot_id=$(echo "${response}" | sed -E 's/.*"snapshotId":"([^"]+)".*/\1/')

if [ -z "${snapshot_id}" ]; then
  echo "snapshotId not found in response"
  exit 1
fi

echo "Fetching snapshots list"
snapshots=$(curl -fsS "${API_URL}/snapshots")
echo "${snapshots}" | grep -q "${snapshot_id}"
seeded_id=$(echo "${snapshots}" | sed -n 's/.*"id":"\\([^"]*\\)","owner":"acme","name":"payments".*/\\1/p' | head -n 1)
review_snapshot_id=${seeded_id:-${snapshot_id}}

echo "Posting file changes"
changes=$(cat <<'JSON'
{
  "changes": [
    {
      "path": "src/api/payments.cs",
      "changeType": "modified",
      "addedLines": 5,
      "deletedLines": 1,
      "rawDiffRef": "s3://yotei-artifacts/e2e/sample.diff"
    }
  ]
}
JSON
)
curl -fsS -X POST "${API_URL}/snapshots/${snapshot_id}/file-changes" \
  -H "Content-Type: application/json" \
  -d "${changes}" >/dev/null

echo "Uploading raw diff"
upload=$(cat <<'JSON'
{
  "path": "src/api/payments.cs",
  "changeType": "modified",
  "addedLines": 5,
  "deletedLines": 1,
  "diff": "@@ -1,1 +1,8 @@\\n+// payment charge flow\\n+var token = request.Headers[\\\"Authorization\\\"];\\n+var email = request.Email;\\n+await httpClient.PostAsync(\\\"https://api.stripe.com/charge\\\", payload);\\n+await queue.PublishAsync(\\\"payments\\\");\\n+await db.SaveChangesAsync();\\n"
}
JSON
)
curl -fsS -X POST "${API_URL}/snapshots/${snapshot_id}/file-changes/upload" \
  -H "Content-Type: application/json" \
  -d "${upload}" >/dev/null

echo "Fetching snapshot details"
detail=$(curl -fsS "${API_URL}/snapshots/${snapshot_id}")
echo "${detail}" | grep -q "src/api/payments.cs"
echo "${detail}" | grep -Eq "db://|s3://yotei-artifacts/"

echo "Building review session"
curl -fsS -X POST "${API_URL}/review-sessions/${review_snapshot_id}/build" >/dev/null

echo "Fetching review summary"
summary=$(curl -fsS "${API_URL}/review-sessions/${review_snapshot_id}/summary")
echo "${summary}" | grep -q "\"changedFilesCount\""
echo "${summary}" | grep -q "\"riskTags\""
echo "${summary}" | grep -q "\"sideEffects\""

echo "Fetching review tree"
tree=$(curl -fsS "${API_URL}/review-sessions/${review_snapshot_id}/change-tree")
echo "${tree}" | grep -q "\"label\":\"Overview\""
echo "${tree}" | grep -q "\"nodeType\":\"file\""
echo "${tree}" | grep -q "\"nodeType\":\"checklist\""
echo "${tree}" | grep -q "\"nodeType\":\"risk\""

echo "Fetching flow graph"
flow=$(curl -fsS "${API_URL}/review-sessions/${review_snapshot_id}/flow")
echo "${flow}" | grep -q "\"nodes\""
echo "${flow}" | grep -q "\"edges\""
echo "${flow}" | grep -q "\"nodeType\":\"entry\""
echo "${flow}" | grep -q "\"nodeType\":\"side_effect\""

node_id=$(echo "${tree}" | tr '{' '\n' | awk -F'"' '/"nodeType":"file"/ { for (i=1; i<=NF; i++) if ($i=="id") { print $(i+2); exit } }')
if [ -z "${node_id}" ]; then
  echo "review node id not found in tree response"
  echo "${tree}"
  exit 1
fi

echo "Fetching behaviour summary"
summary=$(curl -fsS "${API_URL}/review-nodes/${node_id}/behaviour-summary")
echo "${summary}" | grep -q "\"behaviourChange\""

echo "Fetching checklist"
checklist=$(curl -fsS "${API_URL}/review-nodes/${node_id}/checklist")
echo "${checklist}" | grep -q "\"items\""
echo "${checklist}" | grep -qi "idempot"
echo "${checklist}" | grep -qi "timeouts"

echo "Fetching raw diff"
diff_response=$(curl -sS -w "\n%{http_code}" "${API_URL}/review-nodes/${node_id}/diff")
diff_body=$(echo "${diff_response}" | sed '$d')
diff_status=$(echo "${diff_response}" | tail -n 1)
if [ "${diff_status}" -ne 200 ]; then
  echo "raw diff failed with status ${diff_status}"
  echo "${diff_body}"
  exit 1
fi
echo "${diff_body}" | grep -q "stripe"

echo "E2E smoke checks passed"

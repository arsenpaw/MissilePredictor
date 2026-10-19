#!/bin/bash
# PreToolUse guard: Claude must never run tests/MissilePredictor.IntegrationTests.
# Those tests boot the real app and fire the real Home Assistant alarm automation.
# Unit tests (tests/MissilePredictor.Tests) stay allowed.

input=$(cat)
tool=$(jq -r '.tool_name // ""' <<<"$input")

deny() {
  jq -n --arg reason "$1" '{
    hookSpecificOutput: {
      hookEventName: "PreToolUse",
      permissionDecision: "deny",
      permissionDecisionReason: $reason
    }
  }'
  exit 0
}

reason="Integration tests fire the REAL Home Assistant alarm and may only be run by the user. Run unit tests only: dotnet test tests/MissilePredictor.Tests"

# Shell commands: the Bash tool and Rider's terminal tool both pass "command".
cmd=$(jq -r '.tool_input.command // ""' <<<"$input")
if [[ -n "$cmd" ]]; then

  # Running the integration project or its tests (building it is fine).
  if grep -qiE 'IntegrationTests|AlarmFlow|RealHomeAssistant' <<<"$cmd" &&
     grep -qiE 'dotnet[[:space:]]+(test|vstest)|vstest\.console|xunit\.console|testhost|dotnet[[:space:]]+[^[:space:]]+\.dll' <<<"$cmd"; then
    deny "$reason"
  fi

  # A test run that isn't scoped to the unit test project would pick up the
  # solution, and with it the integration project.
  if grep -qE 'dotnet[[:space:]]+(test|vstest)' <<<"$cmd" &&
     ! grep -qE 'MissilePredictor\.Tests([/\\.[:space:]"]|$)' <<<"$cmd"; then
    deny "$reason (a bare or solution-wide 'dotnet test' would include them)"
  fi
else
  # IDE run/debug tools (Rider MCP): block anything pointing at the integration project.
  if grep -qiE 'IntegrationTests|AlarmFlow|RealHomeAssistant' <<<"$(jq -c '.tool_input // {}' <<<"$input")"; then
    deny "$reason"
  fi
fi

exit 0

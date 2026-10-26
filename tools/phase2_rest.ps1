#==================================================================
#  phase2_rest.ps1 - Run the remaining Phase 2 probes in one session
#==================================================================
# Groups M, Q, R through probe_eval.ps1, then keypad, files/Polar, and
# finally restore_state.ps1 to undo test definitions. Needs elevation:
# run via run_elevated.ps1 -Probe phase2_rest.ps1.

foreach ($g in @("M_rpn", "Q_answer", "R_retest"))
{
	"===== group $g"
	& "$PSScriptRoot\probe_eval.ps1" -Group $g
}
"===== keypad"
& "$PSScriptRoot\probe_keypad.ps1"
"===== files and polar"
& "$PSScriptRoot\probe_files.ps1"
"===== restore"
& "$PSScriptRoot\restore_state.ps1"

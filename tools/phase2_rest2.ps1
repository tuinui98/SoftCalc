#==================================================================
#  phase2_rest2.ps1 - Guarded file retests, Polar, Edit Variable, L
#==================================================================
# One elevated session: probe_files2.ps1, then group L again (with
# verified settings), then restore_state.ps1. Run via
# run_elevated.ps1 -Probe phase2_rest2.ps1.

"===== files, polar, edit variable"
& "$PSScriptRoot\probe_files2.ps1"
"===== group L_precision (rerun)"
if (-not (Get-Process -Name PowerCalc -ErrorAction SilentlyContinue))
{
	[void](Start-PowerCalc)
}
& "$PSScriptRoot\probe_eval.ps1" -Group L_precision
"===== restore"
& "$PSScriptRoot\restore_state.ps1"

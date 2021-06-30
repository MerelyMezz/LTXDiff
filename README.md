# LTXDiff

LTXDiff diff [base directory] [mod directory] [relative path to root file]
Lists every section and variable pair of the mod that differs from the base. If a section is present in both the base and the mod, it is marked with a "!" to make it DLTX compliant. Sections that only exist in the mod are copied entirely. Sections and variables that are present in the base directory, but not in the mod, are marked as deleted with "!" for variables and "!!" for sections.

LTXDiff findroot [base directory] [mod directory] [relative path to file]
Trace back include tree to find out which LTX file originally included the specified one.

LTXDiff dltxify [base directory] [mod directory] [mod name]
Applies diff on an entire mod directory automatically and saving a fully deployable version of the mod in "[mod directory]_DLTX".

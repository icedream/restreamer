######################################################
# Part of liquidsoap relay script for r/a/dio aac+ stream
# by Icedream
#
# Read /LICENSE.txt for information about the license.
######################################################

#
# This should fix missing or wrongly-coded metadata.
#
def fixmeta(m) =
	metacmd = "./process-metadata.php " ^quote(json_of(m))
	lines = get_process_lines(metacmd)
	log("Fixed metadata: Artist="^list.nth(lines,0)^",Title="^list.nth(lines,1))
	[("artist",list.nth(lines,0)),("title",list.nth(lines,1))]
end


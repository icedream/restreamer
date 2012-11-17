#!/usr/bin/env liquidsoap

######################################################
# Liquidsoap script for r/a/dio aac+ stream relay
# by Icedream
#
# Read LICENSE.txt for information about the license.
######################################################

%include "config.liq"
%include "lib/audio_processing.liq"
%include "lib/metadata_fix.liq"

# TODO: Make this a working daemon.

####################
# CORE SCRIPT
####################

# Relay original stream
radio = input.http(
	user_agent="Icedream/MobileStreamServer",
	timeout=30.,
	poll_delay=.5,
	new_track_on_metadata=true,
	force_mime="audio/mpeg",
	buffer=.5,
	id="original",
	STREAM_URL
)

# Fix metadata
radio = map_metadata(fixmeta, radio)

# Fallback if stream goes offline
#fb = single(FALLBACK_PATH)
fb = amplify(.1, noise())
fb = rewrite_metadata([
	("title", "r/a/dio Stream down at the moment, stay tuned on r-a-d.io!"),
	("artist", "r/a/dio")
], fb)

# For the weird clips on the original stream
radio = ladspa.declip(id="fx_declip", radio)

# Fallback to offline audio file
radio = fallback(id="fb_fallback2", track_sensitive=false, [radio, fb])

# Make stream safe for shout
radio = mksafe(radio)

#####################
# ACTUAL STREAMING
#####################

# Default settings
shout = output.icecast(
	password=ICECAST_PASSWORD,
	description=ICECAST_DESCRIPTION,
	name=ICECAST_NAME,
	genre=ICECAST_GENRE,
	public=ICECAST_PUBLIC,
	user=ICECAST_USERNAME,
	port=ICECAST_PORT,
	host=ICECAST_HOST
)

# Stream as aac+
shout(
	mount=ICECAST_MOUNT ^ "/aacp",
	%aacplus(bitrate=56), radio
)

# Stream as opus
shout(
	mount=ICECAST_MOUNT ^ "/opus",
	%ogg(%opus(signal="music",vbr="none",bitrate="32",samplerate=44100,application="audio",max_bandwidth="narrow_band")), radio
)

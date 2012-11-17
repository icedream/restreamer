######################################################
# Liquidsoap configuration file for the restreamer
######################################################

####################
# LIQUIDSOAP SETUP
####################

# Some liquidsoap logging configuration
# Refer to the documentation for more info about these
# settings.
set("log.stdout", true)
set("log.file", true)
set("log.file.path", "./<script>.log")

# Original stream URL
STREAM_URL = "http://stream.r-a-d.io:1130/main.mp3"

# Icecast mountpoint/stream settings
ICECAST_HOST = "127.0.0.1"
ICECAST_PORT = 8000
ICECAST_PASSWORD = "donthackme"
ICECAST_USERNAME = "source" #default is "source"
ICECAST_MOUNT = "/r-a-dio"
ICECAST_PUBLIC = false
ICECAST_DESCRIPTION = "r/a/dio mobile aac+ stream"
ICECAST_NAME = "r/a/dio mobile"
ICECAST_GENRE = "Various"

# Stream fallback (yet to be solved properly)
#FALLBACK_PATH = "offline.wav"

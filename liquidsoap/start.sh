#!/bin/sh

######################################################
# Liquidsoap script starter for r/a/dio aac+ stream
# relay
# by Icedream
#
# Read LICENSE.txt for information about the license.
######################################################

# Note: Really primitive. Ultra.

chmod +x lq_restream.liq
/usr/bin/env liquidsoap lq_restream.liq
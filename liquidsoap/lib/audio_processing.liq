######################################################
# Part of liquidsoap relay script for r/a/dio aac+ stream
# by Icedream
#
# Read /LICENSE.txt for information about the license.
######################################################

# TODO: multiband_compress' quality is not the best.

# 5-band compression with very slight bass boost
# @category Source / Sound Processing
# @param s The input source.
def multiband_compress(s)
  eq1 = filter.iir.eq.low(frequency = 168.)
  eq2a = filter.iir.eq.high(frequency = 100.)
  eq2 = filter.iir.eq.low(frequency = 768.)
  eq3a = filter.iir.eq.high(frequency = 680.)
  eq3 = filter.iir.eq.low(frequency = 1800.)
  eq4a = filter.iir.eq.high(frequency = 1366.)
  eq4 = filter.iir.eq.low(frequency = 10000.)
  eq5a = filter.iir.eq.high(frequency = 7508.)

  add(normalize = false,
      [ compress(attack = 100., release = 200., threshold = -2.,
                 ratio = 4., gain = 6., knee = 0.3,
           eq1(s)),
        compress(attack = 100., release = 200., threshold = -5.,
                 ratio = 6., gain = 4.3, knee = 0.3,
           eq2a(eq2(s))),
        compress(attack = 100., release = 200., threshold = -5.,
                 ratio = 6., gain = 5., knee = 0.3,
           eq3a(eq3(s))),
	compress(attack = 100., release = 200., threshold = -5.,
                 ratio = 6., gain = 4.4, knee = 0.3,
           eq4a(eq4(s))),
        compress(attack = 100., release = 200., threshold = -8.,
                 ratio = 6., gain = 4., knee = 0.3,
           eq5a(s))
      ])
end

# VLevel compression
# @category Source / Sound Processing
# @param s The input source.
# Audio processing
def vlevel_compress(s)
	ladspa.vlevel_stereo(
		id="fx_compression",
		look_ahead=2.2,
		maximum_multiplier=10.,
		strength=0.7,
		use_maximum_multiplier=true,
		s)
	 # dyson compression
end

<?php
/**
 * Example configuration file for Restreamer.
 */

// Stream from http://localhost:8000/teststream.mp3
$_SOURCE = Array(
	"host" => "localhost",			// source host
	"port" => 8000,				// source port
	"mountpoint" => "/teststream.mp3",		// source mountpoint, only needed for icecast streams, defaults to "/"
	"relay-metadata" => true,			// true (full relay) or false (content-only relay). SHOULD be set to false for OGG streams if possible
	"headers" => Array(
		"icy-pub" => false			// overrides the "icy-pub" header sent to the targets, forces the stream to be non-public, even if the source
							// says it is public
		// for more information about headers, check the icecast/shoutcast protocol documentations (somewhere in the web)
	)
);

$_TARGETS = Array(
	// Icecast2 Server
	Array(
		"host" => "another.icecastserver.com",
		"port" => 8000,
		"type" => "icecast2",
		"username" => "source",			// default is "source", so you can leave that away
		"password" => "donthackme",
		"mountpoint" => "/teststream"
	),

	// Shoutcast Server
	Array(
		"host" => "another.shoutcastserver.org",
		"port" => 8080,
		"type" => "shoutcast",
		"password" => "i.r.chicken.k"
	)
);
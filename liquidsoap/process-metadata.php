#!/usr/bin/env php
<?php
/******************************************************
 * Metadata fixing script for r/a/dio aac+ stream relay
 * by Icedream
 *
 * Read LICENSE.txt for information about the license.
 ******************************************************/

$m = json_decode($argv[1]);

// Normal metadata parsing
if(!empty($m -> title))
{
	if(empty($m -> artist))
	{
		$i = strpos($m -> title, $s=" - ");
		if($i > 0)
		{
			$m -> artist = substr($m -> title, 0, $i);
			$m -> title = substr($m -> title, $i + strlen($s));
		} else
			$m -> artist = "";
	}
}

// Fallback metadata
if(!empty($m -> title) && $m -> title == "fallback")
{
	$m -> artist = "r/a/dio";
	$m -> title = "r/a/dio stream is down, look at http://r-a-d.io/ or #r/a/dio on Rizon IRC for more info!";
}

// Fill up missing metadata
if(!empty($m -> artist))
	$artist = trim($m -> artist);
else
	$artist = "";

if(!empty($m -> title))
	$title = trim($m -> title);
else
	$title = "r/a/dio";

// Print out fixed metadata
echo "$artist\n$title\n";

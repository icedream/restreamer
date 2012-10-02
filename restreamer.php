<?php
/**
 * Restreamer
 *
 * @author	Carl Kittelberger
 * @email	icedream@blazing.de
 * @version	0.1a
 * @package	restreamer
 */

// Help!
if($argc < 1)
{
	echo 'Usage: ./'.array_pop(explode("/",__FILE__))
		.' [switches]'
		.' <config-file>';
	echo '';
	echo "POSSIBLE SWITCHES:";
	echo "\t-d";
	echo "\t\tEnables debug mode.";
	exit;
}

// Switches
define("DEBUG", in_array("-d", $argv));

// Configuration file
require_once(array_pop($argv));

if(empty($_SOURCE))
{
	error("You need to input the source data into the configuration!");
	exit;
}

if(empty($_TARGETS))
	warn("No targets defined. This will just download the stream for nothing.");

// Connect to the source
$_SOURCE["socket"] = fsockopen($_SOURCE["host"], $_SOURCE["port"], $_SOURCE["errno"], $_SOURCE["error"]);
$s = &$_SOURCE["socket"];
if(empty($_SOURCE["mountpoint"]))
	$_SOURCE["mountpoint"] = "/";
fputs($s, "GET " . $_SOURCE["mountpoint"] . " HTTP/1.1\r\n");
fputs($s, "Host: " . $_SOURCE["host"] . "\r\n");
fputs($s, "User-Agent: GWRestreaming/0.1a\r\n");
fputs($s, "Connection: close\r\n");
if($_SOURCE["relay-metadata"])
	fputs($s, "icy-metadata:1\r\n");
fputs($s, "\r\n");
if(empty($_SOURCE["headers"]))
	$_SOURCE["headers"] = Array();
$l = fgets($s);
if(substr($l, 9, 3) != "200")
{
	echo "Can't connect to the source, HTTP error code " . substr($l,9,3) . "\r\n";
	exit;
}

// Source header analysis
$l = trim(fgets($s));
while($l != "")
{
	list($headName) = explode(":", $l);
	$headName = strtolower(trim($headName));
	$headValue = trim(substr($l, strlen($headName) + 1));
	if(substr($headName, 0, 4) == "icy-" || substr($headName, 0, 4) == "ice-" || $headName=="content-type")
	{
		if($headName == "icy-metaint")
		{
			debug("Given metadata interval is $headValue");
			$_SOURCE["metaint"] = intval($headValue);
		}
		else if(!isset($_SOURCE["headers"][$headName]))
		{
			debug("Received header $headName");
			$_SOURCE["headers"][$headName] = $headValue;
		}
		else debug("Received header $headName, but its value has been fixed by configuration");
	} else debug("Ignoring header $headName");
	$l = trim(fgets($s));
}

// Validation
if($_SOURCE["headers"]["content-type"] == "audio/ogg" && $_SOURCE["relay-metadata"])
{
	//die("This stream is an ogg stream - normally without any metadata packed into the ICY protocol -, means you should set the \"relay-metadata\" option to false.");

	$_SOURCE["relay-metadata"] = false;
	warn("Forcing \"relay-metadata\" to false since stream is in an OGG container providing all metadata outside of the protocol.");
}

// Initialize targets
$connServers = Array();

foreach($_TARGETS as $target)
{
	if(empty($target["username"]))
		$target["username"] = "source";
	if(@$target["relay-metadata"] === null)
		$target["relay-metadata"] = $_SOURCE["relay-metadata"];
	if(empty($target["mountpoint"]))
		$target["mountpoint"] = "/"; // just for the notices

	$target["socket"] = fsockopen($target["host"], $target["port"], $target["errno"], $target["error"], 4);

	// Possible types should be: shoutcast, icecast2
	$target["type"] = strtolower($target["type"]);
	if($target["type"] == "shoutcast") {
		fputs($target["socket"], $target["password"] . "\r\n");
		$status = trim(fgets($target["socket"]));
		if($status != "OK2")
		{
			fclose($target["socket"]);
			error("Could not connect to ".$target["host"].": Status is $status.");
			continue;
		}
		foreach($_SOURCE["headers"] as $name => $value)
			fputs($target["socket"], "$name: $value\r\n");
		fputs($target["socket"], "\r\n");
	} else {
		fputs($target["socket"], "SOURCE " . $target["mountpoint"] . " HTTP/1.1\r\n");
		fputs($target["socket"], "Host: " . $target["host"] . "\r\n");
		fputs($target["socket"], "Authorization: Basic " . base64_encode($target["username"].":".$target["password"])."\r\n");
		foreach($_SOURCE["headers"] as $name => $value)
			fputs($target["socket"], "$name: $value\r\n");
		// Here I experimented a bit with icy-metaint.
		// Pretty interesting that this actually works for source => server.
		// Though the server is dumb enough to just ignore the data.
		fputs($target["socket"], "\r\n");
		$l = fgets($target["socket"]);
		if(strstr($l," 200 ")<=0)
		{
			@fclose($target["socket"]);
			error("Could not connect to ".$target["host"].":".$target["port"].$target["mountpoint"].": Server gave back: $l");
			continue;
		}
	}

	$status = trim(fgets($target["socket"]));
	while($status != "")
	{
		list($capName, $capValue) = explode($status, ":");
		$target["capabilities"][$capName] = trim($capValue);
		debug("Received: $capName = $capValue");
		$status = trim(fgets($ns));
	}

	echo "Successfully connected to ".$target["host"].":".$target["port"].$target["mountpoint"]."\r\n";
	$connServers[] = $target;
}


echo "Now streaming!\r\n";
$_SOURCE["metadata"] = "";
$metadatalength = 0;

// Listening loop
while($_SOURCE["relay-metadata"])
{
	// Receive the metadata!
	$recv = $_SOURCE["metaint"];
	while($recv > 0)
	{
		$packet = fread($_SOURCE["socket"], $recv);
		$recv -= strlen($packet);
		write($packet);
	}
	$metadatalength = fread($_SOURCE["socket"], 1);
	$metadatalength = unpack("Clength", $metadatalength);
	$metadatalength = $metadatalength["length"] * 16;

	// Parse the metadata!
	if($metadatalength > 0)
	{
		$metadata = "";
		$metadatarecv = $metadatalength;
		while($metadatarecv > 0)
		{
			$metadatapacket = fread($_SOURCE["socket"], $metadatarecv);
			$metadatarecv -= strlen($metadatapacket);
			$metadata .= $metadatapacket;
		}
		//echo "$metadatarecv Metadata bytes left\r\n";

		$metadata = trim($metadata);
		$metadata = explode("'", $metadata);
		$metadata = $metadata[1];
		if($metadata != $_SOURCE["metadata"])
		{
			echo "New metadata: $metadata\r\n";
			$_SOURCE["metadata"] = $metadata;
		}
	}
	metadata($metadata);
}

debug("ICY metadata relaying is off.");
while(true)
{
	$p="";
	while(strlen($p)<8192)
		$p.=fread($_SOURCE["socket"], 8192-strlen($p));
	write($p);
}

// TODO: Implement support for Ctrl+C

// Close all connections
foreach($connServers as $a)
	fclose($a["socket"]);
fclose($_SOURCE["socket"]);

echo "Streaming stopped.";


/***************
 * Functions
 ***************/

function debug($content)
{
	if(DEBUG) echo "DEBUG: $content\r\n";
}

function warn($content)
{
	echo "WARN: $content\r\n";
}

function error($content)
{
	echo "ERROR: $content\r\n";
}

function write($content)
{
	global $connServers;
	foreach($connServers as &$a)
	{
		if($a == null)
			continue;
		stream_set_timeout($a["socket"], 0, isset($a["write-timeout"]) ? $a["write-timeout"] : 500);
		if(!fwrite($a["socket"], $content))
		{
/*
			echo "ERROR: Removing " . $a["host"] . ":" . $a["port"].$a["mountpoint"] . " because we got disconnected!\r\n";
			$a = null;
*/
			echo "WARN: Stream error @ ".$a["host"].":".$a["port"].$a["mountpoint"]." - " . $a["error"] . "\r\n";
		}
	}
}

function metadata($content)
{
	global $connServers;
	foreach($connServers as &$a)
	{
		if($a==null) continue;
		
		if((isset($a["metadata"]) && $a["metadata"] == $content) || strlen($content) == 0 /* icecast2's "no metadata change" alias ._. */)
		{
			// Metadata has not been changed

			// I had a little experiment here too thanks to the fact I did not know that the source protocol
			// does not even support sending metadata from source to server in a >direct< way.
			// For example by injecting it the same way the server does for clients. ._.
		} else {
			// Metadata has been changed
			if($a["type"] == "shoutcast")
			{
				// Simple http request
				$response = file_get_contents("http://" . (isset($a["username"]) ? $a["username"] . "@" : "") . $a["host"] . ":" . $a["port"] . "/admin.cgi?pass=" . $a["password"] . "&mode=updinfo&song=" . urlencode($content));
				debug("Shoutcast metadata debug: RESPONSE = $response"); // Expected: nothing since the actual status is in the header. Logic? No, just laziness. ._.
			} else {
				// Simple http request
				$response = file_get_contents("http://" . (isset($a["username"]) ? $a["username"] . ":" : "") . $a["password"] . "@" . $a["host"] . ":" . $a["port"] . "/admin/metadata?mount=" . $a["mountpoint"] . "&mode=updinfo&song=" . urlencode($content));
				debug("Icecast2 metadata debug: RESPONSE = $response"); // Expected: XML code printing that the action was successful (1).
			}
			$a["metadata"] = $content;
		}
	}
}

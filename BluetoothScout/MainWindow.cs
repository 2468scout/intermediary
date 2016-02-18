using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Gtk;
using FRCData;

public partial class MainWindow: Gtk.Window
{
	// where we're storing scout data
	private const string PATH = "/home/cake/FRCScout/data/";
	private List<string> newEntries;

	public MainWindow () : base (Gtk.WindowType.Toplevel) {
		Build ();
		newEntries = new List<string>();
		updateStatus (null);
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)	{
		Application.Quit ();
		a.RetVal = true;
	}

	private void getMatchFeed() {
		WebClient client = new WebClient ();
		var address = new Uri ("http://thebluealliance.com/event/" + feedUrlEntry.Text + "/feed");
		Console.WriteLine ("Got Feed "+address.AbsoluteUri);
		try {
			client.DownloadFile (address, PATH+"matchdata.xml");
			feedErrorLabel.Text = "Downloaded Feed";
		} catch(Exception e) {
			feedErrorLabel.Text = "Couldn't Download";
			Console.WriteLine (e.StackTrace);
		}
	}

	private void updateStatus(string[] entries) {
		// get a list of files and folders from the downloaded directory 
		if(entries == null)
			entries = Linux.Execute("find", PATH)[0].Replace(PATH, "").Split('\n');

		Dictionary<string, Team> teams = new Dictionary<string, Team>();

		for (var i = 0; i < entries.Length; i++) {
			// the line from the shell command
			var entry = entries [i];

			// determine what kind of entry it is
			var isTeam = Regex.IsMatch (entry, "^Team \\d+$");
			var isPit = Regex.IsMatch(entry, "^Team (\\d+)/Team \\1\\.match$");
			var isMatch = Regex.IsMatch(entry, "^Team (\\d+)/Match ((Quals|Semis|Quarters|Finals) )?\\d+\\.match$");

			string number = Regex.Match (entry, "(?<=^Team )\\d+").Value;
			if (isTeam || !teams.ContainsKey(number)) {
				// create a team object
				teams.Add (number,
					new Team () {
						Number = number
					});

			} 
			if (isPit || isMatch) {
				// update attributes of the team object
				if (isPit) {
					teams [number].HasPit = true;

				} else if (isMatch) {

					string matchNumber = Regex.Match (entry, "(?<=Match )((Quals|Semis|Quarters|Finals) )?\\d+").Value;
					teams [number].Matches.Add (matchNumber);
				}
			}

		}

		// print out a tree like view of the teams
		foreach(var team in teams.Values) {
			textView.Buffer.Text += " Team " + team.Number+"\n";

			if (team.HasPit) {
				textView.Buffer.Text += " - Robot Data\n";
			}

			if (team.Matches.Capacity > 0) {
				textView.Buffer.Text += " - Matches\n -- ";
				var first = true;
				foreach (var match in team.Matches) {
					textView.Buffer.Text += (first?"":", ")+match;
					first = false;
				}
				textView.Buffer.Text += "\n";
			}
			textView.Buffer.Text += "\n";
		}
	}

	protected void attemptFind (object sender, EventArgs e) {
		textView.Buffer.Text = "";

		string[] entries = null;

		// get a list of the devices
		var deviceList = Linux.Execute ("adb", "devices")[0].Split('\n');

		if (deviceList.Length == 3) { // defaults to 3 lines of output
			deviceErrorLabel.Text = "No Device Connected";
			return;
		}

		// get existing files
		var existing = new List<string>(Linux.Execute("find", PATH)[0].Replace(PATH, "").Split('\n'));
		// try to fetch the files from the device
		var output = Linux.Execute("adb", "pull /sdcard/FRCScouting/data "+PATH)[1];

		// pattern for getting team files out of the adb command
		Regex pattern = new Regex ("(?<=-> "+PATH+").+(?=\n)");

		// stores all the new entries
		var entriesList = new List<string>();

		var numMatches = pattern.Matches (output).Count;
		var match = pattern.Match (output);
		for (int i = 0; match != null && i < numMatches; i++, match=match.NextMatch()) {
			var entry = match.Value;
			if (!existing.Contains (entry)) {
				entriesList.Add(match.Value);
				newEntries.Add (match.Value);
			}
		}
		entries = entriesList.ToArray();
		deviceErrorLabel.Text = "Retrieved Data";

		if (uploadCheckbox.Active && File.Exists(PATH+"/matchdata.xml")) {
			Linux.Execute("adb", "push "+PATH+"/matchdata.xml /sdcard/FRCScouting/matchdata.xml");
		}

		updateStatus (entries);

	}

	protected void downloadFeed (object sender, EventArgs e) {
		getMatchFeed ();
	}

	protected void uploadNew (object sender, EventArgs e) {
		List<string> remaining = uploadFromList (newEntries);
		newEntries.Clear ();
		updateStatus (remaining.ToArray());
		newEntries = remaining;
	}

	private List<string> uploadFromList(List<string> files) {

		List<string> remaining = new List<string> ();

		foreach (string file in files) {
			Console.WriteLine ("uploading " + file);
			NameValueCollection nvc = new NameValueCollection ();
			string number = Regex.Match (file, "(?<=^Team )\\d+").Value;
			Console.WriteLine (number);
			if (number != "") {
				nvc.Add ("team", number);
			} else {
				nvc.Add ("match", "1");
			}
			var success = HttpUploadFile ("http://localhost/scoutUpload.php", PATH + file, "upload", "text/xml", nvc) == "1";
			if (success) {
				Console.WriteLine ("Success!");
			} else {
				remaining.Add (file);
			}
		}

		return remaining;
	}

	protected void uploadAll (object sender, EventArgs e) {
		string[] entries = Linux.Execute("find", PATH)[0].Replace(PATH, "").Split('\n');
		List<string> allEntries = new List<string> ();

		for (var i = 0; i < entries.Length; i++) {
			var entry = entries [i];

			var isPit = Regex.IsMatch (entry, "^Team (\\d+)/Team \\1\\.match$");
			var isMatch = Regex.IsMatch (entry, "^Team (\\d+)/Match ((Quals|Semis|Quarters|Finals) )?\\d+\\.match$");

			if (isPit || isMatch)
				allEntries.Add (entry);
		}

		uploadFromList (allEntries);
	}

	protected void uploadFeed (object sender, EventArgs e)
	{
		List<string> matchData = new List<string> ();
		matchData.Add ("matchdata.xml");
		List<string> remaining = uploadFromList (matchData);
		feedErrorLabel.Text = "Upload Feed " + (remaining.Capacity == 0 ? "Succeeded" : "Failed");
	}

	class Team {
		public bool HasPit { get; set; }
		public string Number { get; set; }
		public List<string> Matches { get; set; }

		public Team(){
			Matches = new List<string>();
		}
	}

	public static string HttpUploadFile(string url, string file, string paramName, string contentType, NameValueCollection nvc) {
		//Console.WriteLine(string.Format("Uploading {0} to {1}", file, url));
		string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x");
		byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

		HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
		wr.ContentType = "multipart/form-data; boundary=" + boundary;
		wr.Method = "POST";
		wr.KeepAlive = true;
		wr.Credentials = System.Net.CredentialCache.DefaultCredentials;

		Stream rs = wr.GetRequestStream();

		string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
		foreach (string key in nvc.Keys)
		{
			rs.Write(boundarybytes, 0, boundarybytes.Length);
			string formitem = string.Format(formdataTemplate, key, nvc[key]);
			byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
			rs.Write(formitembytes, 0, formitembytes.Length);
		}
		rs.Write(boundarybytes, 0, boundarybytes.Length);

		string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
		string header = string.Format(headerTemplate, paramName, file, contentType);
		//Console.WriteLine (header);
		byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
		rs.Write(headerbytes, 0, headerbytes.Length);

		FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
		byte[] buffer = new byte[4096];
		int bytesRead = 0;
		while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0) {
			rs.Write(buffer, 0, bytesRead);
		}
		fileStream.Close();

		byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
		rs.Write(trailer, 0, trailer.Length);
		rs.Close();

		WebResponse wresp = null;
		try {
			wresp = wr.GetResponse();
			Stream stream2 = wresp.GetResponseStream();
			StreamReader reader2 = new StreamReader(stream2);
			var output = reader2.ReadToEnd();
			//Console.WriteLine("File uploaded, server response is: "+output);
			wr = null;
			return output;
		} catch(Exception ex) {
			Console.WriteLine("Error uploading file", ex);
			if(wresp != null) {
				wresp.Close();
				wresp = null;
			}
			wr = null;
			return "";
		}
	}

}

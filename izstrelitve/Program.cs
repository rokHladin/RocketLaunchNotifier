using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;

// Specify your credentials in config.json file
dynamic config = JsonConvert.DeserializeObject(File.ReadAllText("config.json"));
string senderEmail = config.senderEmail;
string senderPassword = config.senderPassword;

// Get the time for next week
DateTime currentDateTime = DateTime.Now;
DateTime futureDateTime = currentDateTime.AddDays(7);

List<RocketLaunchMoreInfo> newRocketLaunches = new List<RocketLaunchMoreInfo>();
List<RocketLaunchMoreInfo> updatedRocketLaunches = new List<RocketLaunchMoreInfo>();

// Get upcoming rocket launches from the API
var client = new HttpClient();
var request = new HttpRequestMessage(HttpMethod.Get,
    $"https://ll.thespacedevs.com/2.2.0/launch/upcoming/?window_start__gte={currentDateTime.ToString("yyyy-MM-dd")}&window_start__lt={futureDateTime.ToString("yyyy-MM-dd")}");
var response = await client.SendAsync(request);
response.EnsureSuccessStatusCode();

// Connect to the database and create the table if it doesn't exist
using (var connection = new SQLiteConnection("Data Source=rocket_launches.db"))
{
    connection.Open();

    // Create the table if it doesn't exist
    string createTableQuery = @"CREATE TABLE IF NOT EXISTS RocketLaunches (
                                Id INTEGER PRIMARY KEY,
                                Name TEXT NOT NULL,
                                Date TEXT NOT NULL)";
    using (var command = new SQLiteCommand(createTableQuery, connection))
    {
        command.ExecuteNonQuery();
    }

    var jsonString = await response.Content.ReadAsStringAsync();

    var launches = JObject.Parse(jsonString)["results"];
    foreach (var launch in launches)
    {
        DateTime launchStartDateTime = DateTime.Parse(launch["window_start"].ToString());
        DateTime launchEndDateTime = DateTime.Parse(launch["window_end"].ToString());

        RocketLaunch formattedLaunch = new RocketLaunch
        {
            Name = launch["name"].ToString(),
            Date = launchStartDateTime
        };

        bool isNewLaunch = CheckIfNewLaunch(connection, formattedLaunch);

        if (isNewLaunch)
        {
            Console.WriteLine("New launch detected: " + formattedLaunch.Name);
            // Add new launch to the database
            string insertQuery = "INSERT INTO RocketLaunches (Name, Date) VALUES (@Name, @Date)";
            using (var insertCommand = new SQLiteCommand(insertQuery, connection))
            {
                insertCommand.Parameters.AddWithValue("@Name", formattedLaunch.Name);
                insertCommand.Parameters.AddWithValue("@Date", formattedLaunch.Date);
                insertCommand.ExecuteNonQuery();
            }

            // Add new launch to the list
            newRocketLaunches.Add(new RocketLaunchMoreInfo
            {
                Name = launch["name"].ToString(),
                Start = launchStartDateTime,
                End = launchEndDateTime,
                Description = launch["mission"]["description"].ToString(),
                ImageUrl = launch["image"].ToString()
            });

        }
        else if (CheckIfUpdatedLaunch(connection, formattedLaunch))
        {
            Console.WriteLine("Updated launch detected: " + formattedLaunch.Name);
            // Add updated launch to the list
            updatedRocketLaunches.Add(new RocketLaunchMoreInfo
            {
                Name = launch["name"].ToString(),
                Start = launchStartDateTime,
                End = launchEndDateTime,
                Description = launch["mission"]["description"].ToString(),
                ImageUrl = launch["image"].ToString()
            });
        }


    }
    // Create the HTML-formatted email message for new and updated rocket launches
    string htmlMessage = @"
<!DOCTYPE html>
<html>
<head>
    <style>
        body { font-family: Arial, sans-serif; color: black; }
        table { border-collapse: collapse; width: 100%; }
        th, td { border: 1px solid #ddd; padding: 8px; }
        th { background-color: #4CAF50; color: white; }
        img { width: 100px; height: auto; } /* Adjust size as needed */
    </style>
</head>
<body>";
    if (newRocketLaunches.Count > 0)
    {
        htmlMessage += @"

    <h2>Upcoming Rocket Launches This Week</h2>
        <tr>
            <th>Name</th>
            <th>Start Window</th>
            <th>End Window</th>
            <th>Mission Description</th>
            <th>Image</th>
        </tr>";
        // Add new launches to the email message
        foreach (RocketLaunchMoreInfo launch in newRocketLaunches)
        {
            htmlMessage += $@"
        <tr>
            <td><b>{launch.Name}</b></td>
            <td>{launch.Start.ToString("MMMM dd, yyyy hh:mm tt")}</td>
            <td>{launch.End.ToString("MMMM dd, yyyy hh:mm tt")}</td>
            <td>{launch.Description}</td>
            <td><img src='{launch.ImageUrl}' alt='Launch Image'></td>
        </tr>";
        }
    }
    if (updatedRocketLaunches.Count > 0)
    {

        htmlMessage += @"
    </table>
    <h2>Updated Rocket Launches This Week</h2>
    <table>
        <tr>
            <th>Name</th>
            <th>Start Window</th>
            <th>End Window</th>
            <th>Mission Description</th>
            <th>Image</th>
        </tr>";
        // Add updated launches to the email message
        foreach (RocketLaunchMoreInfo launch in updatedRocketLaunches)
        {
            htmlMessage += $@"
        <tr>
            <td><b>{launch.Name}</b></td>
            <td>{launch.Start.ToString("MMMM dd, yyyy hh:mm tt")}</td>
            <td>{launch.End.ToString("MMMM dd, yyyy hh:mm tt")}</td>
            <td>{launch.Description}</td>
            <td><img src='{launch.ImageUrl}' alt='Launch Image'></td>
        </tr>";
        }

    }
    // We can always add this part, because otherwise the email wont get sent
    htmlMessage += @"
    </table>
</body>
</html>";


    // SMTP server details, change these to your own
    string smtpServer = "smtp.gmail.com";
    int smtpPort = 587; 
    if (newRocketLaunches.Count > 0 || updatedRocketLaunches.Count > 0)
    {
        try
        {
            // Read email recipients from JSON file
            List<string> recipients = ReadRecipientsFromJson("recipients.json");

            // Create a new MailMessage
            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(senderEmail);
            mail.Subject = "HTML Formatted Email";
            mail.Body = htmlMessage;
            mail.IsBodyHtml = true; // Set to true for HTML-formatted email

            // Add recipients to the MailMessage object
            foreach (string recipientEmail in recipients)
            {
                mail.To.Add(recipientEmail);
            }

            // Create an SMTP client
            SmtpClient smtpClient = new SmtpClient(smtpServer);
            smtpClient.Port = smtpPort;
            smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);
            smtpClient.EnableSsl = true;

            // Send the email
            smtpClient.Send(mail);

            Console.WriteLine($"Email sent successfully to {recipients.Count} recipients.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    // Read email recipients from JSON file
    static List<string> ReadRecipientsFromJson(string filePath)
    {
        List<string> recipients = new List<string>();

        try
        {
            // Read JSON file content
            string json = File.ReadAllText(filePath);

            // Deserialize JSON to list of strings
            recipients = JsonConvert.DeserializeObject<List<string>>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error reading JSON file: " + ex.Message);
        }

        return recipients;
    }
}


static bool CheckIfNewLaunch(SQLiteConnection connection, RocketLaunch launch)
{
    // Preveri ali je izstrelitev že shranjena v bazi
    string query = "SELECT COUNT(*) FROM RocketLaunches WHERE Name = @Name AND Date = @Date";
    using (var command = new SQLiteCommand(query, connection))
    {
        command.Parameters.AddWithValue("@Name", launch.Name);
        command.Parameters.AddWithValue("@Date", launch.Date);
        int count = Convert.ToInt32(command.ExecuteScalar());
        return count == 0;
    }
}

static bool CheckIfUpdatedLaunch(SQLiteConnection connection, RocketLaunch launch)
{
    // Preveri ali je izstrelitev že shranjena v bazi
    string query = "SELECT COUNT(*) FROM RocketLaunches WHERE Name = @Name AND Date != @Date";
    using (var command = new SQLiteCommand(query, connection))
    {
        command.Parameters.AddWithValue("@Name", launch.Name);
        command.Parameters.AddWithValue("@Date", launch.Date);
        int count = Convert.ToInt32(command.ExecuteScalar());
        return count > 0;
    }
}

class RocketLaunch
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
}

class RocketLaunchMoreInfo
{
    public string Name { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
}
# Rocket Launch Tracker

This program tracks upcoming rocket launches for the next week. It fetches data from the [The Space Devs API](https://ll.thespacedevs.com/2.2.0/launch/upcoming/) and stores it in a local SQLite database. When the program is run, it sends an email to all the recipients listed in the `recipients.json` file with the upcoming or changed rocket launches.

## Setup

1. Clone the repository to your local machine.
2. Create a `config.json` file in the root directory of the project with the following structure:

```json
{
    "senderEmail": "your-email@example.com",
    "senderPassword": "your-email-password"
}
```
3. Create a `recipients.json` file in the root directory of the project with the following structure:

```json
[
	"receiver1@example.com",
	"receiver2@example.com"
	"receiver3@example.com"
	"receiver4@example.com"
]
```
4. Run the program and all the recipients will receive an email with the upcoming rocket launches.
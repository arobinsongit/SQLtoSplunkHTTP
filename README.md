# SQL to Splunk HTTP

=================

A console application to read data from a Microsoft SQL Server database and forward to a Splunk Instance via the HTTP Collector.

## Motivation

In the world of Industrial Automation there are treasure troves of information hidden away in SQL server databases.  These might be operational logs, alarm logs, or user event logs.  Unfortunately for the average user pulling this data together for search and analysis is usually beyond their skillset.

I tried used the Splunk DBConnect and found it to be quite a bit less than friendly.  After fighting with odd errors and Java (enough said) I decided to pursue another option by writing my own.

Working with my contacts at Splunk I was directed to the new HTTP Event Collector for sending data to Splunk.  I wasn't a big fan of the provided library so I wrote a drastically simplified version for my needs.  My situation is unique in that I don't have to worry about retries and caching data in flight.  By keeping a record of the last index or timestamp that was retrieved we can always go back and query all of the data that has been written since the last successful transmission.

## Usage

The application is a single EXE that can be run from the command line with appropriate command line switches.  If no command line switches are provided the application looks for an options.json file in the local directory to read in configuration.

### Command Line Switches

From the command line execute the application EXE with any of the following command line switches: -?/-h/--help.  This will display a list of the available command line switches as well as subcommands.

```batchfile
c:\local\SQLtoSplunkHTTP\SQLtoSplunkHTTP\bin\Debug>SQLtoSplunkHTTP.exe -?

SQL Server to Splunk HTTP Collector 0

Usage: SQLToSplunkHTTP [options] [command]

Options:
  -?| -h| --help            Show help information
  -v| --version             Show version information
  -o| --optionsfile <PATH>  Path to options file (Optional)

Commands:
  clearcache                Deletes the current cache file
  createdefaultoptionsfile  Create a default options.json file

Use "SQLToSplunkHTTP [command] --help" for more information about a command.
```

### Commands

Commands provide a way to run a specific function contained in the application.

Executing a subcommand looks like this:

```batchfile
c:\local\SQLtoSplunkHTTP\SQLtoSplunkHTTP\bin\Debug>SQLtoSplunkHTTP.exe clearcache
Starting c:\local\SQLtoSplunkHTTP\SQLtoSplunkHTTP\bin\Debug\SQLtoSplunkHTTP.exe Version 1.1.0.0
Deleting cache file WonderwareAlarms-EventStampUTC.txt

```

To understand the command in more detail utilize a help command line switch in combination with the command like this:

```batchfile
c:\local\SQLtoSplunkHTTP\SQLtoSplunkHTTP\bin\Debug>SQLtoSplunkHTTP.exe createdefaultoptionsfile -?

Usage: SQLToSplunkHTTP createdefaultoptionsfile [options]

Options:
  -?| -h| --help         Show help information
  -o| --overwrite        Overwrite existing options.json file
  -f| --filename <PATH>  Name of options file (Optional)
```

### Running As a Service


### Logs



## Testing

No testing written yet.  We plan to use NUnit for a testing framework.

## TODO List

Check out the [Issues](/../../issues) List

## Contributing

Check out the [Contributing](/CONTRIBUTING.MD) file

## Contributors

* [Andy Robinson](mailto:andy@phase2automation.com), Principal of [Phase 2 Automation](http://phase2automation.com).
* See list of [Contributors](/../../graphs/contributors) on the repo for others

## Credit

Thanks to Brian Gilmore (@BrianMGilmore) for pointing me in the direciton of the new HTTP collector.

## License

Apache License. See the [LICENSE file](/LICENSE) for details.
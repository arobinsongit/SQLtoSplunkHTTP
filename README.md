# SQL to Splunk HTTP

=================

A console application to read data from a Microsoft SQL Server database and forward to a Splunk Instance via the HTTP Collector.

## Motivation

In the world of Industrial Automation there are treasure troves of information hidden away in SQL server databases.  These might be operational logs, alarm logs, or user event logs.  Unfortunately for the average user pulling this data together for search and analysis is usually beyond their skillset.

I tried used the Splunk DBConnect and found it to be quite a bit less than friendly.  After fighting with odd errors and Java (enough said) I decided to pursue another option by writing my own.

Working with my contacts at Splunk I was directed to the new HTTP Event Collector for sending data to Splunk.  I wasn't a big fan of the provided library so I wrote a drastically simplified version for my needs.  My situation is unique in that I don't have to worry about retries and caching data in flight.  By keeping a record of the last index or timestamp that was retrieved we can always go back and query all of the data that has been written since the last successful transmission.

## Usage

The application can be run with no options or you can view options with the -?/-h/--help command line switches.  I have also done some very basic testing using NSSM to run the application as a service with positive results.

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
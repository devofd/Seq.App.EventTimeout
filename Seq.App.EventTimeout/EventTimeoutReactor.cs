﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EventTimeout
{
    [SeqApp("Event Timeout", Description = "Super-powered monitoring of Seq events with start/end times, timeout and suppression intervals, matching multiple properties, day of week and day of month inclusion/exclusion, and optional holiday API!")]
    public class EventTimeoutReactor : SeqApp, ISubscribeTo<LogEventData>
    {
        // Count of matches
        int _matched;
        int _lastMatched;
        bool _cannotMatchAlerted;
        bool _skippedShowtime;

        string startFormat = "H:mm:ss";
        string endFormat = "H:mm:ss";

        DateTime _startTime;
        DateTime _endTime;
        DateTime _lastLog;
        DateTime _lastCheck;
        DateTime _lastMatchLog;
        DateTime _lastDay;

        bool _isUpdating;
        DateTime _lastUpdate;
        DateTime _lastError;
        int _errorCount;

        List<DayOfWeek> _daysOfWeek;
        List<int> _includeDays;
        List<int> _excludeDays;
        string _testDate;

        TimeSpan _timeOut;
        bool _repeatTimeout;
        TimeSpan _suppressionTime;

        Dictionary<string, string> _properties;

        string _alertMessage;
        string _alertDescription;
        LogEventLevel _timeoutLogLevel;
        bool _isAlert;
        System.Timers.Timer _timer;
        const int _retryCount = 10;
        bool _isShowtime;
        bool _includeApp;
        bool _diagnostics;

        bool _isTags;
        string[] _tags;

        bool _useHolidays;
        string _country;
        string _apiKey;
        List<string> _holidayMatch;
        List<string> _localeMatch;
        bool _includeWeekends;
        bool _includeBank;
        bool _useProxy;
        string _proxy;
        bool _bypassLocal;
        string[] _localAddresses;
        string _proxyUser;
        string _proxyPass;
        List<AbstractApiHolidays> _holidays;

        [SeqAppSetting(
            DisplayName = "Diagnostic logging",
            HelpText = "Send extra diagnostic logging to the stream.")]
        public bool Diagnostics { get; set; }

        [SeqAppSetting(
            DisplayName = "Start time",
            HelpText = "The time (H:mm:ss, 24 hour format) to start monitoring.")]
        public string StartTime { get; set; }

        [SeqAppSetting(
            DisplayName = "End time",
            HelpText = "The time (H:mm:ss, 24 hour format) to stop monitoring, up to 24 hours after start time.")]
        public string EndTime { get; set; }
                
        [SeqAppSetting(
            DisplayName = "Timeout interval (seconds)",
            HelpText = "Time period in which a matching log entry must be seen. After this, an alert will be raised.",
            InputType = SettingInputType.Integer)]
        public int Timeout { get; set; }

        [SeqAppSetting(
            DisplayName = "Repeat timeout",
            HelpText = "Optionally re-arm the timeout after a match, within the start/end time parameters - useful for a 'heartbeat' style alert.",
            IsOptional = true)]
        public bool RepeatTimeout { get; set; }

        [SeqAppSetting(
            DisplayName = "Days of week",
            HelpText = "Comma-delimited - Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday.",
            IsOptional = true)]
        public string DaysOfWeek { get; set; }

        [SeqAppSetting(
            DisplayName = "Include days of month",
            HelpText = "Only run on these days. Comma-delimited - first,last,first weekday,last weekday,first-fourth sunday-saturday,1-31.",
            IsOptional = true)]
        public string IncludeDaysOfMonth { get; set; }

        [SeqAppSetting(
            DisplayName = "Exclude days of month",
            HelpText = "Never run on these days. Comma-delimited - first,last,1-31.",
            IsOptional = true)]
        public string ExcludeDaysOfMonth { get; set; }

        [SeqAppSetting(
            DisplayName = "Suppression interval (seconds)",
            HelpText = "If an alert has been raised, further alerts will be suppressed for this time. Will also suppress repeating timeout matches.",
            InputType = SettingInputType.Integer)]
        public int SuppressionTime { get; set; }

        [SeqAppSetting(DisplayName = "Log level for timeouts",
          HelpText = "Verbose, Debug, Information, Warning, Error, Fatal.",
          IsOptional = true)]
        public string TimeoutLogLevel { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 1 name",
            HelpText = "Case insensitive property name (must be a full match). If not configured, the @Message property will be used. If this is not seen in the configured timeout, an alert will be raised.",
            IsOptional = true)]
        public string Property1Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 1 match",
            HelpText = "Case insensitive text to match - partial match okay. If not configured, ANY text will match. If this is not seen in the configured timeout, an alert will be raised.",
            IsOptional = true)]
        public string TextMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 2 name",
            HelpText = "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property2Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 2 match",
            HelpText = "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property2Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 3 name",
            HelpText = "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property3Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 3 match",
            HelpText = "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property3Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 4 name",
            HelpText = "Case insensitive property name (must be a full match). If not configured, this will not be evaluated.",
            IsOptional = true)]
        public string Property4Name { get; set; }

        [SeqAppSetting(
            DisplayName = "Property 4 match",
            HelpText = "Case insensitive text to match - partial match okay. If property name is set and this is not configured, ANY text will match.",
            IsOptional = true)]
        public string Property4Match { get; set; }

        [SeqAppSetting(
            DisplayName = "Alert message",
            HelpText = "Event message to raise.")]
        public string AlertMessage { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert description",
            HelpText = "Optional description associated with the event raised.")]
        public string AlertDescription { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Alert tags",
            HelpText = "Tags for the event, separated by commas.")]
        public string Tags { get; set; }

        [SeqAppSetting(
            DisplayName = "Include instance name in alert message",
            HelpText = "Prepend the instance name to the alert message.")]
        public bool IncludeApp { get; set; }


        [SeqAppSetting(
            DisplayName = "Holidays - use Holidays API for public holiday detection",
            HelpText = "Connect to the AbstractApi Holidays service to detect public holidays.")]
        public bool UseHolidays { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - Country code",
            HelpText = "Two letter country code (eg. AU).",
            IsOptional = true)]
        public string Country { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - API key",
            HelpText = "Sign up for an API key at https://www.abstractapi.com/holidays-api and enter it here.",
            IsOptional = true,
            InputType = SettingInputType.Password)]
        public string ApiKey { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - match these holiday types",
            HelpText = "Comma-delimited list of holiday types (eg. National, Local) - case insensitive, partial match okay.",
            IsOptional = true)]
        public string HolidayMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - match these locales",
            HelpText = "Holidays are valid if the location matches one of these comma separated values (eg. Australia,New South Wales) - case insensitive, must be a full match.",
            IsOptional = true)]
        public string LocaleMatch { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - include weekends",
            HelpText = "Include public holidays that are returned for weekends.")]
        public bool IncludeWeekends { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - include Bank Holidays.",
            HelpText = "Include bank holidays")]
        public bool IncludeBank { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - test date",
            HelpText = "yyyy-M-d format. Used only for diagnostics - should normally be empty.",
            IsOptional = true)]
        public string TestDate { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy address",
            HelpText = "Proxy address for Holidays API.",
            IsOptional = true)]
        public string Proxy { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy bypass local addresses",
            HelpText = "Bypass local addresses for proxy.")]
        public bool BypassLocal { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - local addresses for proxy bypass",
            HelpText = "Local addresses to bypass, comma separated.",
            IsOptional = true)]
        public string LocalAddresses { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy username",
            HelpText = "Username for proxy authentication.",
            IsOptional = true)]
        public string ProxyUser { get; set; }

        [SeqAppSetting(
            DisplayName = "Holidays - proxy password",
            HelpText = "Username for proxy authentication.",
            IsOptional = true,
            InputType = SettingInputType.Password)]
        public string ProxyPass { get; set; }

        protected override void OnAttached()
        {
            LogEvent(LogEventLevel.Debug, "Check {AppName} diagnostic level ({Diagnostics}) ...", App.Title, Diagnostics);
            _diagnostics = Diagnostics;

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Check include {AppName} ({IncludeApp}) ...", App.Title, IncludeApp);
            _includeApp = IncludeApp;
            if (!_includeApp && _diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will not be included in alert message ...", App.Title);
            else if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "App name {AppName} will be included in alert message ...", App.Title);

            DateTime testStartTime;
            if (!DateTime.TryParseExact(StartTime, "H:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out testStartTime))
                if (DateTime.TryParseExact(StartTime, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out testStartTime))
                    startFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug, "Start Time {StartTime} does  not parse to a valid DateTime - app will exit ...", StartTime);

            DateTime testEndTime;
            if (!DateTime.TryParseExact(EndTime, "H:mm:ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out testEndTime))
                if (DateTime.TryParseExact(EndTime, "H:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out testEndTime))
                    endFormat = "H:mm";
                else
                    LogEvent(LogEventLevel.Debug, "End Time {EndTime} does  not parse to a valid DateTime - app will exit ...", EndTime);

            LogEvent(LogEventLevel.Debug, "Use Holidays API {UseHolidays}, Country {Country}, Has API key {IsEmpty} ...", UseHolidays, Country, !string.IsNullOrEmpty(ApiKey));
            setHolidays();
            retrieveHolidays(DateTime.Today, DateTime.UtcNow);

            if (!_useHolidays || _isUpdating)
                utcRollover(DateTime.UtcNow);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Timeout {timeout} to TimeSpan ...", Timeout);
            _timeOut = TimeSpan.FromSeconds(Timeout);
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Timeout is {timeout} ...", _timeOut.TotalSeconds);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Repeat Timeout: {RepeatTimeout} ...", RepeatTimeout);
            _repeatTimeout = RepeatTimeout;

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Suppression {suppression} to TimeSpan ...", SuppressionTime);
            _suppressionTime = TimeSpan.FromSeconds(SuppressionTime);
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Parsed Suppression is {timeout} ...", _suppressionTime.TotalSeconds);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Days of Week {daysofweek} to UTC Days of Week ...", DaysOfWeek);

            _daysOfWeek = Dates.getDaysOfWeek(DaysOfWeek, StartTime, startFormat);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "UTC Days of Week {daysofweek} will be used ...", _daysOfWeek.ToArray());

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Include Days of Month {includedays} ...", IncludeDaysOfMonth);
            _includeDays = Dates.getDaysOfMonth(IncludeDaysOfMonth, StartTime, startFormat);
            if (_includeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {includedays} ...", _includeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: ALL ...");

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Exclude Days of Month {excludedays} ...", ExcludeDaysOfMonth);
            _excludeDays = Dates.getDaysOfMonth(ExcludeDaysOfMonth, StartTime, startFormat);
            if (_excludeDays.Count > 0)
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {excludedays} ...", _excludeDays.ToArray());
            else
                LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: NONE ...");

            //Evaluate the properties we will match
            _properties = setProperties();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Match criteria will be: {MatchText}", PropertyMatch.matchConditions(_properties));

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Message '{AlertMessage}' ...", AlertMessage);
            _alertMessage = string.IsNullOrWhiteSpace(AlertMessage) ? "An event timeout has occurred!" : AlertMessage.Trim();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Message '{AlertMessage}' will be used ...", _alertMessage);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Alert Description '{AlertDescription}' ...", AlertDescription);
            _alertDescription = string.IsNullOrWhiteSpace(AlertDescription) ? _alertMessage + " : Generated by Seq " + Host.BaseUri : AlertDescription.Trim();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Alert Description '{AlertDescription}' will be used ...", _alertDescription);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Convert Tags '{Tags}' to array ...", Tags);
            _tags = (Tags ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToArray();
            if (_tags.Length > 0)
                _isTags = true;

            if (string.IsNullOrWhiteSpace(TimeoutLogLevel))
            {
                TimeoutLogLevel = "Error";
            }
            if (!Enum.TryParse<LogEventLevel>(TimeoutLogLevel, out _timeoutLogLevel))
                _timeoutLogLevel = LogEventLevel.Error;
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Log level {loglevel} will be used for timeouts on {Instance} ...", _timeoutLogLevel, App.Title);

            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Starting timer ...");
            _timer = new System.Timers.Timer(1000);
            _timer.AutoReset = true;
            _timer.Elapsed += TimerOnElapsed;
            _timer.Start();
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Timer started ...");

        }


        private void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            DateTime timeNow = DateTime.UtcNow;
            DateTime localDate = DateTime.Today;
            if (!string.IsNullOrEmpty(_testDate))
                localDate = DateTime.ParseExact(_testDate, "yyyy-M-d", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);

            if (_lastDay < localDate)
                retrieveHolidays(localDate, timeNow);

            //We can only enter showtime if we're not currently retrying holidays, but existing showtimes will continue to monitor
            if ((!_useHolidays || (_isShowtime || (!_isShowtime && !_isUpdating))) && timeNow >= _startTime && timeNow < _endTime)
            {
                if (!_isShowtime && (!_daysOfWeek.Contains(_startTime.DayOfWeek) || (_includeDays.Count > 0 && !_includeDays.Contains(_startTime.Day)) || _excludeDays.Contains(_startTime.Day)))
                {
                    //Log that we have skipped a day due to an exclusion
                    if (!_skippedShowtime)
                        LogEvent(LogEventLevel.Debug, "Matching will not be performed due to exclusions - Day of Week Excluded {DayOfWeek}, Day Of Month Not Included {IncludeDay}, Day of Month Excluded {ExcludeDay} ...",
                            !_daysOfWeek.Contains(_startTime.DayOfWeek), _includeDays.Count > 0 && !_includeDays.Contains(_startTime.Day), _excludeDays.Count > 0 && _excludeDays.Contains(_startTime.Day));
                    _skippedShowtime = true;
                }
                else
                {
                    //Showtime! - Evaluate whether we have matched properties with log events
                    if (!_isShowtime)
                    {
                        LogEvent(LogEventLevel.Debug, "UTC Start Time {Time} ({DayOfWeek}), monitoring for {MatchText} within {Timeout} seconds, until UTC End time {EndTime} ({EndDayOfWeek}) ...",
                            _startTime.ToShortTimeString(), _startTime.DayOfWeek, PropertyMatch.matchConditions(_properties), _timeOut.TotalSeconds, _endTime.ToShortTimeString(), _endTime.DayOfWeek);
                        _isShowtime = true;
                        _lastCheck = timeNow;
                        _lastMatchLog = timeNow;
                    }

                    TimeSpan difference = timeNow - _lastCheck;
                    //Check the timeout versus any successful matches. If repeating timeouts are enabled, we'll compare matched with lastMatched to detect if there's been any matches
                    if (difference.TotalSeconds > _timeOut.TotalSeconds && (_matched == 0 || (_repeatTimeout && _matched == _lastMatched)))
                    {
                        TimeSpan suppressDiff = timeNow - _lastLog;
                        if (_isAlert && suppressDiff.TotalSeconds < _suppressionTime.TotalSeconds)
                            return;

                        //Log event                    
                        LogEvent(_timeoutLogLevel, "{Message} : {Description}", _alertMessage, _alertDescription);
                        _lastLog = timeNow;
                        _isAlert = true;
                    }

                    //Grab a snapshot of the match count for next evaluation
                    _lastMatched = _matched;
                }
            }
            else if (timeNow < _startTime || timeNow >= _endTime)
            {
                //Showtime can end even if we're retrieving holidays
                if (_isShowtime)
                    LogEvent(LogEventLevel.Debug, "UTC End Time {Time} ({DayOfWeek}), no longer monitoring for {MatchText}, total matches {Matches} ...", _endTime.ToShortTimeString(), _endTime.DayOfWeek, PropertyMatch.matchConditions(_properties), _matched);

                //Reset the match counters
                _lastLog = timeNow;
                _lastCheck = timeNow;
                _lastMatchLog = timeNow;
                _matched = 0;
                _lastMatched = 0;
                _isAlert = false;
                _isShowtime = false;
                _cannotMatchAlerted = false;
                _skippedShowtime = false;
            }

            //We can only do UTC rollover if we're not currently retrying holidays and it's not during showtime
            if (!_isShowtime && (!_useHolidays || !_isUpdating) && (_startTime <= timeNow && string.IsNullOrEmpty(_testDate)))
            {
                utcRollover(timeNow);
                //Take the opportunity to refresh include/exclude days to allow for month rollover
                _includeDays = Dates.getDaysOfMonth(IncludeDaysOfMonth, StartTime, startFormat);
                if (_includeDays.Count > 0)
                    LogEvent(LogEventLevel.Debug, "Include UTC Days of Month: {includedays} ...", _includeDays.ToArray());
                _excludeDays = Dates.getDaysOfMonth(ExcludeDaysOfMonth, StartTime, startFormat);
                if (_excludeDays.Count > 0)
                    LogEvent(LogEventLevel.Debug, "Exclude UTC Days of Month: {excludedays} ...", _excludeDays.ToArray());
            }
        }

        public void On(Event<LogEventData> evt)
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            DateTime timeNow = DateTime.UtcNow;
            DateTime localDate = DateTime.Today;
            bool cannotMatch = false;
            List<string> cannotMatchProperties = new List<string>();
            int properties = 0;
            int matches = 0;

            if ((_matched == 0 || _repeatTimeout) && _isShowtime)
            {
                foreach (KeyValuePair<string, string> property in _properties)
                {
                    properties++;
                    if (property.Key.Equals("@Message", StringComparison.OrdinalIgnoreCase))
                    {
                        if (PropertyMatch.matches(evt.Data.RenderedMessage, property.Value))
                            matches++;

                    }
                    else
                    {
                        bool matchedKey = false;

                        //IReadOnlyDictionary ContainsKey is case sensitive, so we need to iterate
                        foreach (KeyValuePair<string, object> key in evt.Data.Properties)
                            if (key.Key.Equals(property.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                matchedKey = true;
                                if (PropertyMatch.matches(evt.Data.Properties[property.Key].ToString(), property.Value))
                                {
                                    matches++;
                                    break;
                                }
                            }

                        //If one of the configured properties doesn't have a matching property on the event, we won't be able to raise an alert
                        if (!matchedKey)
                        {
                            cannotMatch = true;
                            cannotMatchProperties.Add(property.Key);
                        }
                    }
                }

                if (cannotMatch && !_cannotMatchAlerted)
                {
                    LogEvent(LogEventLevel.Debug, "Warning - An event was seen without the properties {PropertyName}, which may impact the ability to alert on failures - further failures will not be logged ...", string.Join(",", cannotMatchProperties.ToArray()));
                    _cannotMatchAlerted = true;
                }

                //If all configured properties were present and had matches, log an event
                if (!cannotMatch && properties == matches)
                {
                    _matched++;
                    int matchCount = _matched;
                    int lastMatch = _lastMatched;

                    TimeSpan difference = timeNow - _lastMatchLog;
                    _lastCheck = timeNow;

                    //Allow for repeating timeouts
                    if (!_repeatTimeout)
                        LogEvent(LogEventLevel.Debug, "Successfully matched {TextMatch}! Further matches will not be logged ...", PropertyMatch.matchConditions(_properties));
                    else
                    {
                        if (lastMatch == 0 || difference.TotalSeconds > _suppressionTime.TotalSeconds)
                        {
                            _lastMatchLog = timeNow;
                            //Only log one event regardless of how many match the first event                        
                            if (lastMatch == 0 && _matched == 1 || _lastMatched > 0)
                            LogEvent(LogEventLevel.Debug, "Successfully matched {TextMatch}! Total matches {Total} - resetting timeout to {Timeout} seconds, further matches will not be logged for {Suppression} seconds ...", PropertyMatch.matchConditions(_properties),
                                _matched, _timeOut.TotalSeconds, _suppressionTime.TotalSeconds);
                        }
                    }
                }
            }
        }

        private Dictionary<string, string> setProperties()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            //Property 1 is mandatory, and will be @Message unless PropertyName is overriden
            KeyValuePair<string, string> property = getProperty(1, Property1Name, TextMatch);
            properties.Add(property.Key, property.Value);
            property = getProperty(2, Property2Name, Property2Match);
            if (!string.IsNullOrEmpty(property.Key))
                properties.Add(property.Key, property.Value);
            property = getProperty(3, Property3Name, Property3Match);
            if (!string.IsNullOrEmpty(property.Key))
                properties.Add(property.Key, property.Value);
            property = getProperty(4, Property4Name, Property4Match);
            if (!string.IsNullOrEmpty(property.Key))
                properties.Add(property.Key, property.Value);

            return properties;
        }

        private KeyValuePair<string, string> getProperty(int property, string propertyName, string propertyMatch)
        {
            if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Validate Property {PropertyNo}: '{PropertyNameValue}' ...", property, propertyName);
            KeyValuePair<string, string> propertyResult = PropertyMatch.getProperty(property, propertyName, propertyMatch);

            if (!string.IsNullOrEmpty(propertyResult.Key) && _diagnostics)
                if (!string.IsNullOrEmpty(propertyMatch))
                    LogEvent(LogEventLevel.Debug, "Property {PropertyNo} '{PropertyName}' will be used to match '{PropertyMatch}'...", property, propertyResult.Key, propertyResult.Value);
                else
                    LogEvent(LogEventLevel.Debug, "Property {PropertyNo} '{PropertyName}' will be used to match ANY text ...", property, propertyResult.Key);
            else if (_diagnostics)
                LogEvent(LogEventLevel.Debug, "Property {PropertyNo} will not be used to match values ...", property);

            return propertyResult;
        }

        private void setHolidays()
        {
            if (UseHolidays && !string.IsNullOrEmpty(Country) && !string.IsNullOrEmpty(ApiKey))
            {
                if (_diagnostics)
                    LogEvent(LogEventLevel.Debug, "Validate Country {Country}", Country);
                if (Holidays.validateCountry(Country))
                {
                    _useHolidays = true;
                    _country = Country;
                    _apiKey = ApiKey;
                    _includeWeekends = IncludeWeekends;
                    _includeBank = IncludeBank;

                    if (string.IsNullOrEmpty(HolidayMatch))
                        _holidayMatch = new List<string>();
                    else
                        _holidayMatch = HolidayMatch.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
                    if (string.IsNullOrEmpty(LocaleMatch))
                        _localeMatch = new List<string>();
                    else
                        _localeMatch = LocaleMatch.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

                    if (!string.IsNullOrEmpty(Proxy))
                    {
                        _useProxy = true;
                        _proxy = Proxy;
                        _bypassLocal = BypassLocal;
                        _localAddresses = LocalAddresses.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();
                        _proxyUser = ProxyUser;
                        _proxyPass = ProxyPass;
                    }

                    if (_diagnostics)
                        LogEvent(LogEventLevel.Debug, "Holidays API Enabled: {UseHolidays}, Country {Country}, Use Proxy {UseProxy}, Proxy Address {Proxy}, BypassLocal {BypassLocal}, Authentication {Authentication} ...", _useHolidays, _country,
                            _useProxy, _proxy, _bypassLocal, !string.IsNullOrEmpty(ProxyUser) && !string.IsNullOrEmpty(ProxyPass));
                    WebClient.setFlurlConfig(App.Title, _useProxy, _proxy, _proxyUser, _proxyPass, _bypassLocal, _localAddresses);
                }
                else
                {
                    _useHolidays = false;
                    LogEvent(LogEventLevel.Debug, "Holidays API Enabled: {UseHolidays}, Could not parse country {CountryCode} to valid region ...", _useHolidays, _country);
                }
            }
            else if (UseHolidays)
            {
                _useHolidays = false;
                LogEvent(LogEventLevel.Debug, "Holidays API Enabled: {UseHolidays}, One or more parameters not set", _useHolidays);
            }

            _lastDay = DateTime.Today.AddDays(-1);
            _lastError = DateTime.Now.AddDays(-1);
            _lastUpdate = DateTime.Now.AddDays(-1);
            _errorCount = 0;
            _testDate = TestDate;
            _holidays = new List<AbstractApiHolidays>();
        }

        private void retrieveHolidays(DateTime localDate, DateTime utcDate)
        {
            if (_useHolidays && (!_isUpdating || (_isUpdating && (DateTime.Now - _lastUpdate).TotalSeconds > 10 && (DateTime.Now - _lastError).TotalSeconds > 10 && _errorCount < _retryCount)))
            {
                _isUpdating = true;
                if (!string.IsNullOrEmpty(_testDate))
                    localDate = DateTime.ParseExact(_testDate, "yyyy-M-d", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None);

                if (_diagnostics)
                    LogEvent(LogEventLevel.Debug, "Retrieve holidays for {Date}, Last Update {lastUpdateDate} {lastUpdateTime} ...", localDate.ToShortDateString(), _lastUpdate.ToShortDateString(), _lastUpdate.ToShortTimeString());
                string holidayUrl = WebClient.getUrl(_apiKey, _country, localDate);
                if (_diagnostics)
                    LogEvent(LogEventLevel.Debug, "URL used is {url} ...", holidayUrl);
                try
                {
                    _lastUpdate = DateTime.Now;
                    List<AbstractApiHolidays> result = WebClient.getHolidays(_apiKey, _country, localDate).Result;
                    _holidays = Holidays.validateHolidays(result, _holidayMatch, _localeMatch, _includeBank, _includeWeekends);
                    _lastDay = localDate;
                    _errorCount = 0;

                    if (_diagnostics && !string.IsNullOrEmpty(_testDate))
                    {
                        LogEvent(LogEventLevel.Debug, "Test date {testDate} used, raw holidays retrieved {testCount} ...", _testDate, result.Count);
                        foreach (AbstractApiHolidays holiday in result)
                            LogEvent(LogEventLevel.Debug, "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                                holiday.name, holiday.name_local, holiday.localStart, holiday.utcStart, holiday.utcEnd, holiday.type, holiday.location, holiday.locations.ToArray());
                    }

                    LogEvent(LogEventLevel.Debug, "Holidays retrieved and validated {holidayCount} ...", _holidays.Count);
                    foreach (AbstractApiHolidays holiday in _holidays)
                        LogEvent(LogEventLevel.Debug, "Holiday Name: {Name}, Local Name {LocalName}, Start {LocalStart}, Start UTC {Start}, End UTC {End}, Type {Type}, Location string {Location}, Locations parsed {Locations} ...",
                            holiday.name, holiday.name_local, holiday.localStart, holiday.utcStart, holiday.utcEnd, holiday.type, holiday.location, holiday.locations.ToArray());

                    _isUpdating = false;
                    if (!_isShowtime)
                        utcRollover(utcDate, true);
                }
                catch (Exception ex)
                {
                    _errorCount++;
                    LogEvent(LogEventLevel.Debug, ex, "Error {Error} retrieving holidays, public holidays cannot be evaluated (Try {Count} of {retryCount})...", ex.Message, _errorCount, _retryCount);
                    _lastError = DateTime.Now;
                }

            }
            else if (!_useHolidays || (_isUpdating && _errorCount >= 10))
            {
                _isUpdating = false;
                _lastDay = localDate;
                _errorCount = 0;
                _holidays = new List<AbstractApiHolidays>();
                if (_useHolidays && !_isShowtime)
                    utcRollover(utcDate, true);
            }
        }

        private void utcRollover(DateTime utcDate, bool isUpdateHolidays = false)
        {
            //Day rollover, we need to ensure the next start and end is in the future
            if (!string.IsNullOrEmpty(_testDate))
                _startTime = DateTime.ParseExact(_testDate + " " + StartTime, "yyyy-M-d " + startFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None).ToUniversalTime();
            else
                _startTime = DateTime.ParseExact(StartTime, startFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None).ToUniversalTime();

            //If we updated holidays, don't automatically put start time to the future
            if (_startTime < utcDate && !isUpdateHolidays)
                _startTime = _startTime.AddDays(1);

            //If there are holidays, account for them
            foreach (AbstractApiHolidays holiday in _holidays)
                if (_startTime >= holiday.utcStart && _startTime < holiday.utcEnd)
                {
                    _startTime = _startTime.AddDays(1);
                    break;
                }

            if (!string.IsNullOrEmpty(_testDate))
                _endTime = DateTime.ParseExact(_testDate + " " + EndTime, "yyyy-M-d " + endFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None).ToUniversalTime();
            else
                _endTime = DateTime.ParseExact(EndTime, endFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None).ToUniversalTime();

            if (_endTime <= _startTime)
                if (_endTime.AddDays(1) < _startTime)
                    _endTime = _endTime.AddDays(2);
                else
                    _endTime = _endTime.AddDays(1);

            if (isUpdateHolidays)
                LogEvent(LogEventLevel.Debug, "UTC Day Rollover (Holidays Updated), Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})...",
                StartTime, _startTime.ToShortTimeString(), _startTime.DayOfWeek, EndTime, _endTime.ToShortTimeString(), _endTime.DayOfWeek);
            else
                LogEvent(LogEventLevel.Debug, "UTC Day Rollover, Parse {LocalStart} To Next UTC Start Time {StartTime} ({StartDayOfWeek}), Parse {LocalEnd} to UTC End Time {EndTime} ({EndDayOfWeek})...",
                        StartTime, _startTime.ToShortTimeString(), _startTime.DayOfWeek, EndTime, _endTime.ToShortTimeString(), _endTime.DayOfWeek);
        }

        private void LogEvent(LogEventLevel logLevel, string message, params object[] args)
        {
            List<object> logArgsList = args.ToList();

            if (_includeApp)
            {
                message = "[{AppName}] -" + message;
                logArgsList.Insert(0, App.Title);
            }

            object[] logArgs = logArgsList.ToArray();

            if (_isTags)
                Log.ForContext("Tags", _tags).ForContext("AppName", App.Title).Write((Serilog.Events.LogEventLevel)logLevel, message, logArgs);
            else
                Log.ForContext("AppName", App.Title).Write((Serilog.Events.LogEventLevel)logLevel, message, logArgs);
        }

        private void LogEvent(LogEventLevel logLevel, Exception exception, string message, params object[] args)
        {
            List<object> logArgsList = args.ToList();

            if (_includeApp)
            {
                message = "[{AppName}] -" + message;
                logArgsList.Insert(0, App.Title);
            }

            object[] logArgs = logArgsList.ToArray();

            if (_isTags)
                Log.ForContext("Tags", _tags).ForContext("AppName", App.Title).Write((Serilog.Events.LogEventLevel)logLevel, exception, message, logArgs);
            else
                Log.ForContext("AppName", App.Title).Write((Serilog.Events.LogEventLevel)logLevel, exception, message, logArgs);

        }
    }
}

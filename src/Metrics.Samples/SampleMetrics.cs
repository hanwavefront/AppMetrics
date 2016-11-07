﻿using System;
using System.Collections.Generic;
using System.Threading;
using App.Metrics;
using App.Metrics.Core;
using App.Metrics.Data;

namespace Metrics.Samples
{
    public class SampleMetrics
    {
        /// <summary>
        ///     count the current concurrent requests
        /// </summary>
        private readonly ICounter _concurrentRequestsCounter;

        /// <summary>
        ///     keep a histogram of the input data of our request method
        /// </summary>
        private readonly IHistogram _histogramOfData;

        /// <summary>
        ///     measure the rate at which requests come in
        /// </summary>
        private readonly IMeter _meter;

        private readonly ICounter _setCounter;

        private readonly IMeter _setMeter;

        /// <summary>
        ///     measure the time rate and duration of requests
        /// </summary>
        private readonly ITimer _timer;

        /// <summary>
        ///     keep the total count of the requests
        /// </summary>
        private readonly ICounter _totalRequestsCounter;

        private static IMetricsContext _metricsContext;

        private double _someValue = 1;

        public SampleMetrics(IMetricsContext metricsContext)
        {
            _metricsContext = metricsContext;
            _concurrentRequestsCounter = _metricsContext.Advanced.Counter(SampleMetricsRegistry.Counters.ConcurrentRequestsCounter);
            _histogramOfData = _metricsContext.Advanced.Histogram(SampleMetricsRegistry.Histograms.ResultsExample);
            _meter = _metricsContext.Advanced.Meter(SampleMetricsRegistry.Meters.Requests);
            _setCounter = _metricsContext.Advanced.Counter(SampleMetricsRegistry.Counters.SetCounter);
            _setMeter = _metricsContext.Advanced.Meter(SampleMetricsRegistry.Meters.SetMeter);
            _timer = _metricsContext.Advanced.Timer(SampleMetricsRegistry.Timers.Requests);
            _totalRequestsCounter = _metricsContext.Advanced.Counter(SampleMetricsRegistry.Counters.Requests);

            // define a simple gauge that will provide the instant value of someValue when requested
            _metricsContext.Gauge(SampleMetricsRegistry.Gauges.DataValue, () => _someValue);


            _metricsContext.Gauge(SampleMetricsRegistry.Gauges.CustomRatioGauge, 
                () => ValueReader.GetCurrentValue(_totalRequestsCounter).Count / ValueReader.GetCurrentValue(_meter).FiveMinuteRate);
            
            _metricsContext.Advanced.Gauge(SampleMetricsRegistry.Gauges.Ratio, () => new HitRatioGauge(_meter, _timer, m => m.OneMinuteRate));
        }

        public void Request(int i)
        {
            var multiContextMetrics = new MultiContextMetrics(_metricsContext);
            multiContextMetrics.Run();

            for (var j = 0; j < 5; j++)
            {
                var multiContextInstanceMetrics = new MultiContextInstanceMetrics("Sample Instance " + i.ToString(), _metricsContext);
                multiContextInstanceMetrics.Run();
            }

            using (_timer.NewContext(i.ToString())) // measure until disposed
            {
                _someValue *= (i + 1); // will be reflected in the gauge 

                _concurrentRequestsCounter.Increment(); // increment concurrent requests counter

                _totalRequestsCounter.Increment(); // increment total requests counter 

                _meter.Mark(); // signal a new request to the meter

                _histogramOfData.Update(new Random().Next(5000), i.ToString()); // update the histogram with the input data

                var item = "Item " + new Random().Next(5);
                _setCounter.Increment(item);

                _setMeter.Mark(item);

                // simulate doing some work
                var ms = Math.Abs((int)(new Random().Next(3000)));
                Thread.Sleep(ms);

                _concurrentRequestsCounter.Decrement(); // decrement number of concurrent requests
            }
        }


        public void RunSomeRequests()
        {
            var tasks = new List<Thread>();
            for (var i = 0; i < 10; i++)
            {
                var j = i;
                tasks.Add(new Thread(() => Request(j)));
            }

            tasks.ForEach(t => t.Start());
            tasks.ForEach(t => t.Join());
        }
    }
}
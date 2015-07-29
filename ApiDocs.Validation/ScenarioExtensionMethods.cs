﻿namespace ApiDocs.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ApiDocs.Validation.Error;
    using ApiDocs.Validation.Http;
    using ApiDocs.Validation.Json;
    using ApiDocs.Validation.Params;
    using Newtonsoft.Json.Linq;

    internal static class InternalScenarioExtensionMethods
    {

        /// <summary>
        /// Verify that the expectations in the scenario are met by the response
        /// </summary>
        /// <param name="scenario"></param>
        /// <param name="actualResponse"></param>
        /// <param name="detectedErrors"></param>
        public static void ValidateExpectations(this ScenarioDefinition scenario, HttpResponse actualResponse, List<ValidationError> detectedErrors)
        {
            if (scenario == null) throw new ArgumentNullException("scenario");
            if (actualResponse == null) throw new ArgumentNullException("actualResponse");
            if (detectedErrors == null) throw new ArgumentNullException("detectedErrors");

            var expectations = scenario.Expectations;
            if (null == expectations || expectations.Count == 0)
                return;

            foreach (string key in expectations.Keys)
            {
                string keyIndex;
                var type = BasicRequestDefinition.LocationForKey(key, out keyIndex);
                object expectedValues = expectations[key];
                switch (type)
                {
                    case PlaceholderLocation.Body:
                        ExpectationSatisfied(key, actualResponse.Body, expectedValues, detectedErrors);
                        break;

                    case PlaceholderLocation.HttpHeader:
                        ExpectationSatisfied(key, actualResponse.Headers[keyIndex].FirstOrDefault(), expectedValues, detectedErrors);
                        break;

                    case PlaceholderLocation.Json:
                        try
                        {
                            object value = JsonPath.ValueFromJsonPath(actualResponse.Body, keyIndex);
                            ExpectationSatisfied(key, value, expectedValues, detectedErrors);
                        }
                        catch (Exception ex)
                        {
                            detectedErrors.Add(new ValidationError(ValidationErrorCode.JsonParserException, null, ex.Message));
                        }
                        break;

                    case PlaceholderLocation.Invalid:
                    case PlaceholderLocation.StoredValue:
                    case PlaceholderLocation.Url:
                        detectedErrors.Add(new ValidationWarning(ValidationErrorCode.InvalidExpectationKey, null, "The expectation key {0} is invalid. Supported types are Body, HttpHeader, and JsonPath.", key));
                        break;
                }
            }
        }

        /// <summary>
        /// Check to see if an expectation is met.
        /// </summary>
        /// <param name="key">The name of the expectation being checked.</param>
        /// <param name="actualValue">The value for the expectation to check.</param>
        /// <param name="expectedValues">Can either be a single value or an array of values that are considered valid.</param>
        /// <param name="detectedErrors">A collection of validation errors that will be added to when errors are found.</param>
        /// <returns></returns>
        private static bool ExpectationSatisfied(string key, object actualValue, object expectedValues, List<ValidationError> detectedErrors)
        {
            if (null == key) throw new ArgumentNullException("key");
            if (null == detectedErrors) throw new ArgumentNullException("detectedErrors");

            if (actualValue == null && expectedValues != null)
            {
                detectedErrors.Add(new ValidationError(ValidationErrorCode.ExpectationConditionFailed, null, "Expectation {0}={1} failed. Actual value was null and a value was expected.", key, expectedValues));
                return false;
            }

            if (expectedValues == null)
            {
                return true;
            }


            if (null != (expectedValues as IList<JToken>))
            {
                if (((IList<JToken>)expectedValues).Any(possibleValue => JsonPath.TokenEquals(possibleValue, actualValue)))
                {
                    return true;
                }
            }
            else
            {
                var token = expectedValues as JToken;
                if (token != null)
                {
                    if (JsonPath.TokenEquals(token, actualValue))
                        return true;
                }
                else if (actualValue.Equals(expectedValues))
                {
                    return true;
                }
            }

            detectedErrors.Add(new ValidationError(ValidationErrorCode.ExpectationConditionFailed, null, "Expectation {0} = {1} failed. Actual value: {2}", key, expectedValues, actualValue));
            return false;
        }
    }
}

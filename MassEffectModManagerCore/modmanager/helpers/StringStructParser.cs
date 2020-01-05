using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace MassEffectModManagerCore.modmanager.helpers
{
    /// <summary>
    /// Parser class for different types of string structs and list
    /// </summary>
    [Localizable(false)]
    public class StringStructParser
    {
        /// <summary>
        /// Gets a list of strings that are split by ;
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static List<string> GetSemicolonSplitList(string inputString)
        {
            inputString = inputString.Trim('(', ')');
            return inputString.Split(';').ToList();
        }

        /// <summary>
        /// Gets a dictionary of command split value keypairs. Can accept incoming string with 1 or 2 () outer parenthesis
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static Dictionary<string, string> GetCommaSplitValues(string inputString)
        {
            if (inputString[0] == '(' && inputString[1] == '(' && inputString[inputString.Length - 1] == ')' && inputString[inputString.Length - 2] == ')')
            {
                throw new Exception("GetCommaSplitValues() can only deal with items encapsulated in a single ( ) set. The current set has at least two, e.g. ((value)).");
            }
            inputString = inputString.Trim('(', ')');

            //Find commas
            int propNameStartPos = 0;
            int lastEqualsPos = -1;

            int openingQuotePos = -1; //quotes if any
            int closingQuotePos = -1; //quotes if any
            bool isInQuotes = false;

            int openingParenthesisPos = -1; //parenthesis if any
            int closingParenthesisPos = -1; //parenthesis if any
            int openParenthesisCount = 0;
            Dictionary<string, string> values = new Dictionary<string, string>();
            for (int i = 0; i < inputString.Length; i++)
            {
                switch (inputString[i])
                {
                    case ')':
                        if (openParenthesisCount <= 0)
                        {
                            throw new Exception("ASSERT ERROR: StringStructParser cannot handle closing ) without an opening (.");
                        }
                        //closingParenthesisPos = i;
                        openParenthesisCount--;
                        break;
                    case '(':
                        openParenthesisCount++;
                        break;
                    case '"':
                        if (openingQuotePos != -1)
                        {
                            closingQuotePos = i;
                            isInQuotes = false;
                        }
                        else
                        {
                            openingQuotePos = i;
                            isInQuotes = true;
                        }
                        break;
                    case '=':
                        if (!isInQuotes && openParenthesisCount <= 0)
                        {
                            lastEqualsPos = i;
                        }
                        break;
                    case ',':
                        if (!isInQuotes && openParenthesisCount <= 0)
                        {
                            //New property
                            {
                                if (lastEqualsPos < propNameStartPos) throw new Exception("ASSERT ERROR: Error parsing string struct: equals cannot come before property name start. Value: " + inputString);
                                string propertyName = inputString.Substring(propNameStartPos, lastEqualsPos - propNameStartPos).Trim();
                                string value = "";
                                if (openingQuotePos >= 0)
                                {
                                    value = inputString.Substring(openingQuotePos + 1, closingQuotePos - (openingQuotePos + 1)).Trim();
                                }
                                else
                                {
                                    value = inputString.Substring(lastEqualsPos + 1, i - (lastEqualsPos + 1)).Trim();
                                }
                                values[propertyName] = value;
                            }
                            //Reset values
                            propNameStartPos = i + 1;
                            lastEqualsPos = -1;
                            openingQuotePos = -1; //quotes if any
                            closingQuotePos = -1; //quotes if any
                        }
                        break;
                    //todo: Ignore quoted items to avoid matching a ) on quotes
                    default:

                        //do nothing
                        break;
                }
            }
            //Finish last property
            {
                string propertyName = inputString.Substring(propNameStartPos, lastEqualsPos - propNameStartPos).Trim();
                string value = "";
                if (openingQuotePos >= 0)
                {
                    value = inputString.Substring(openingQuotePos + 1, closingQuotePos - (openingQuotePos + 1)).Trim();
                }
                else
                {
                    value = inputString.Substring(lastEqualsPos + 1, inputString.Length - (lastEqualsPos + 1)).Trim();
                }
                values[propertyName] = value;
            }
            return values;
        }

        /// <summary>
        /// Gets a list of parenthesis splitvalues - items such as (...),(...),(...), the list of ... items are returned.
        /// </summary>
        /// <param name="inputString"></param>
        /// <returns></returns>
        public static List<string> GetParenthesisSplitValues(string inputString)
        {
            //Trim ends if this is a list as ( ) will encapsulte a list of ( ) values, e.g. ((hello),(there)) => (hello),(there)
            if (inputString.Length >= 4)
            {
                if (inputString[0] == '(' && inputString[1] == '(' && inputString[^1] == ')' && inputString[^2] == ')')
                {
                    //Debug.WriteLine(inputString);
                    inputString = inputString.Substring(1, inputString.Length - 2);
                    //Debug.WriteLine(inputString);
                }
            }
            //Debug.WriteLine(inputString);
            //Find matching parenthesis
            Stack<(char c, int pos)> parenthesisStack = new Stack<(char c, int pos)>();
            List<string> splits = new List<string>();
            bool quoteOpen = false;
            for (int i = 0; i < inputString.Length; i++)
            {
                //Debug.WriteLine(inputString[i]);
                switch (inputString[i])
                {
                    case '(':
                        if (!quoteOpen)
                        {
                            parenthesisStack.Push((inputString[i], i));
                        }

                        break;
                    case ')':
                        if (!quoteOpen)
                        {
                            if (parenthesisStack.Count == 0)
                            {
                                Log.Error("Error parsing parenthesis split list: Found closing parenthesis that does not match open parenthesis at position " + i);
                                throw new Exception("Error parsing parenthesis split list: Found closing parenthesis that does not match open parenthesis at position " + i);
                            }

                            var popped = parenthesisStack.Pop();
                            if (parenthesisStack.Count == 0)
                            {
                                //Matching brace found
                                string splitval = inputString.Substring(popped.pos, i - popped.pos + 1);
                                //Debug.WriteLine(splitval);

                                splits.Add(splitval); //This will include the ( )
                            }
                        }

                        break;
                    case '\"':
                        //Used to ignore ( ) inside of a quoted string
                        quoteOpen = !quoteOpen;
                        break;
                }
            }
            if (parenthesisStack.Count > 0)
            {
                Log.Error("Error parsing parenthesis split list: count of open and closing parenthesis does not match.");
                throw new Exception("Unclosed opening parenthesis encountered while parsing parenthesis split list");
            }
            return splits;
        }
    }
}

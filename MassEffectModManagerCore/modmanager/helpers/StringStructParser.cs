using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectModManager.modmanager.helpers
{
    public class StringStructParser
    {
        public static string GetStringProperty(string inputString, string propertyName, bool isQuoted)
        {
            Debug.WriteLine(inputString);
            return null;
        }

        public static List<string> GetSemicolonSplitList(string inputString)
        {
            inputString = inputString.Trim('(', ')');
            return inputString.Split(';').ToList();
        }

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
            Dictionary<string, string> values = new Dictionary<string, string>();
            for (int i = 0; i < inputString.Length; i++)
            {
                switch (inputString[i])
                {
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
                        if (!isInQuotes)
                        {
                            lastEqualsPos = i;
                        }
                        break;
                    case ',':
                        if (!isInQuotes)
                        {
                            //New property
                            {
                                if (lastEqualsPos < propNameStartPos) throw new Exception("ASSERT ERROR: Error prasing string struct: equals cannot come before property name start.");
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

        public static List<string> GetParenthesisSplitValues(string inputString)
        {
            //Trim ends if this is a list as ( ) will encapsulte a list of ( ) values, e.g. ((hello),(there)) => (hello),(there)
            if (inputString.Length >= 4)
            {
                if (inputString[0] == '(' && inputString[1] == '(' && inputString[inputString.Length - 1] == ')' && inputString[inputString.Length - 2] == ')')
                {
                    //Debug.WriteLine(inputString);
                    inputString = inputString.Substring(1, inputString.Length - 3);
                    //Debug.WriteLine(inputString);
                }
            }

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
                        parenthesisStack.Push((inputString[i], i));
                        break;
                    case ')':
                        Debug.WriteLine($"Found ending ) at {i})");
                        var popped = parenthesisStack.Pop();
                        if (parenthesisStack.Count == 0)
                        {
                            //Matching brace found
                            string splitval = inputString.Substring(popped.pos, i - popped.pos + 1);
                            Debug.WriteLine(splitval);

                            splits.Add(splitval); //This will include the ( )
                        }
                        break;
                    case '\"':
                        quoteOpen = !quoteOpen;
                        break;
                    //todo: Ignore quoted items to avoid matching a ) on quotes
                    default:

                        //do nothing
                        break;
                }
            }
            return splits;
        }
    }
}

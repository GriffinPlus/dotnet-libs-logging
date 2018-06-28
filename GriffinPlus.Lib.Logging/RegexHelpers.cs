///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Linq;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Some helper methods around working with regular expressions.
	/// </summary>
	internal class RegexHelpers
	{
		/// <summary>
		/// Checks whether the specified string is a wildcard expression
		/// </summary>
		/// <param name="expression">String to check.</param>
		/// <returns>true, if the specified string is a wildcard expression; otherwise false.</returns>
		public static bool IsWildcardExpression(string expression)
		{
			return expression.Any(c => c == '?' || c == '*');
		}

		/// <summary>
		/// Converts the specified wildcard expression to a regular expression.
		/// </summary>
		/// <param name="expression">Wildcard expression to convert.</param>
		/// <param name="regexOptions">Options to apply when creating the Regex.</param>
		/// <returns>A regular expression matching the same text as the wildcard expression.</returns>
		public static Regex FromWildcardExpression(string expression, RegexOptions regexOptions = RegexOptions.Singleline)
		{
			string regex = "^" + Regex.Escape(expression).Replace("\\*", ".*").Replace("\\?", ".") + "$"; // greedy
			return new Regex(regex, regexOptions);
		}

	}
}

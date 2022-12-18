using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using static mdbook.ErrorHandler;

namespace mdbook
{
	public static class Translations
	{
		static readonly Dictionary<string, Dictionary<string, string>> allTranslations = new Dictionary<string, Dictionary<string, string>>();
		static Dictionary<string, string>? currentTranslations;
		static bool hasTranslations = false;

		public static bool TrySetLanguage(string languageCode, out string error)
		{
			if(!hasTranslations)
			{
				error = string.Empty;
				return true;
			}

			if(!allTranslations.TryGetValue(languageCode.ToUpper(), out currentTranslations))
			{
				error = "Could not find a language called " + languageCode;
				return false;
			}

			error = "";
			return true;
		}

		public static string Get(string key)
		{
			if (!hasTranslations)
				return CreateError(key);

			if (currentTranslations == null || !currentTranslations.TryGetValue(key, out var value))
				return CreateError(key);

			return value;
		}


		public static string Apply(string raw)
		{
			if (!hasTranslations || currentTranslations == null)
				return raw;

			foreach(var keyPair in currentTranslations)
				raw = raw.Replace(keyPair.Key, keyPair.Value);
			
			return raw;
		}


		public static bool TryLoad(string translationsFile, out string error)
		{
			// Do nothing if no value was given
			if (translationsFile == string.Empty)
			{
				error = "";
				return true;
			}

			// Validate file
			if(Path.GetExtension(translationsFile) != ".csv")
			{
				error = "Invalid translation file, expected a .csv file";
				return false;
			}
			if (!File.Exists(translationsFile))
			{
				error = "Invalid translation file path";
				return false;
			}

			// Parse file
			hasTranslations = true;
			using (var csvParser = new TextFieldParser(translationsFile))
			{
				csvParser.CommentTokens = new string[] { "//" };
				csvParser.SetDelimiters(new string[] { "," });
				csvParser.HasFieldsEnclosedInQuotes = true;

				// Load headers
				var headerFields = csvParser.ReadFields();
				if(headerFields == null)
				{
					error = "Could not load the translation headers";
					return false;
				}
				if (headerFields.Length == 0 || headerFields[0].Trim().ToLower() != "key")
				{
					error = "No key found in the first translation header cell";
					return false;
				}
				if (headerFields.Length == 1)
				{
					error = "No languages found in the translation headers";
					return false;
				}

				for(int i = 1; i < headerFields.Length; i++)
				{
					var languageCode = headerFields[i].Trim().ToUpper();
					allTranslations[languageCode] = new Dictionary<string, string>();
				}
				var indexedTranslations = allTranslations.Select(x => x.Value!).ToArray();

				// Load values
				var row = 1;
				var alreadyParsedKeys = new HashSet<string>();
				while (!csvParser.EndOfData)
				{
					row++;
					var fields = csvParser.ReadFields();
					if (fields == null || fields.Length == 0)
						continue;

					var key = fields[0];
					if (alreadyParsedKeys.Contains(key))
					{
						error = $"Translation row {row} (key {key}) is a duplicate, that key already exists";
						return false;
					}
					alreadyParsedKeys.Add(key);

					if (fields.Length > headerFields.Length)
					{
						error = $"Translation row {row} (key {key}), has {fields.Length} cells but only {headerFields.Length} header cells exist";
						return false;
					}

					for(int i = 1; i < fields.Length; i++)
					{
						var language = indexedTranslations[i - 1];
						language[key] = fields[i];
					}
				}
			}

			error = "";
			return true;
		}
	}
}

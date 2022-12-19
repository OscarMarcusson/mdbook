using System.Diagnostics;
using System.Reflection;
using System.Text;
using static mdbook.ErrorHandler;

namespace mdbook
{
	internal class Program
	{
		static readonly Assembly assembly = Assembly.GetEntryAssembly()!;
		static readonly string version = assembly.GetName().Version.ToString();

		static void Main(string[] args)
		{
			if(args.Length == 0)
			{
				Console.WriteLine($"mdbook {version}");
				Console.WriteLine($"");
				Console.WriteLine($"-r | --realtime         Keeps running mdbook on any changes");
				Console.WriteLine($"-s | --style [path]     The CSS file to use, if any");
				Console.WriteLine($"-i | --input [path]     The input directory or markdown file to compile");
			}
			else
			{
				// Read arguments
				var sourceFolder = "";
				var sourceFile = "";
				var outputFile = "";
				var styleFile = "";
				var realtime = false;
				var language = "";
				var translationsFile = "";

				for (var i = 0; i < args.Length; i++)
				{
					switch (args[i])
					{
						case "-l":
						case "--language":
							if (!TryGetValue(ref i, args, ref language, "Expected a language code")) return;
							break;

						case "-t":
						case "--translation":
							if (!TryGetPath(ref i, args, ref translationsFile)) return;
							break;

						case "-r":
						case "--realtime":
							if (realtime)
							{
								Error($"{args[i]} already set");
								return;
							}
							realtime = true;
							break;

						case "-s":
						case "--style":
							if (!TryGetPath(ref i, args, ref styleFile)) return;
							break;

						case "-i":
						case "--input":
							if (!TryGetPath(ref i, args, ref sourceFile)) return;
							break;

						case "-o":
						case "--output":
							if (!TryGetPath(ref i, args, ref outputFile)) return;
							break;

						default:
							if(sourceFile != String.Empty)
							{
								Error("Unexpected argument: " + args[i]);
								return;
							}
							break;
					}
				}

				// Validate arguments
				if(!Translations.TryLoad(translationsFile, out var translationError))
				{
					Error(translationError);
					return;
				}

				if (!string.IsNullOrWhiteSpace(language))
				{
					if(!Translations.TrySetLanguage(language, out var setLanguageError))
					{
						Error(setLanguageError);
						return;
					}
				}

				if (string.IsNullOrWhiteSpace(sourceFile) || (!Directory.Exists(sourceFile) && !File.Exists(sourceFile)))
				{
					Error("Empty input");
					return;
				}

				if (!File.Exists(sourceFile))
				{
					if (Directory.Exists(sourceFile))
					{
						sourceFolder = sourceFile;
						sourceFile = "*.md";
					}
					else
					{
						Error("Invalid input, path not found");
						return;
					}
				}
				else
				{
					sourceFolder = Path.GetDirectoryName(sourceFile);
					sourceFile = Path.GetFileName(sourceFile);
				}
				
				if(!string.IsNullOrWhiteSpace(styleFile) && !File.Exists(styleFile))
				{
					Error("Invalid style, path not found");
					return;
				}

				// Run program


				var styling = styleFile != string.Empty
					? $"<style>\n{StripCSS(styleFile)}</style>"
					: ""
					;

				var content = GetContent(sourceFolder, sourceFile);

				var name = "Test";

				var builder = new StringBuilder();
				builder.AppendLine("<!DOCTYPE html>");
				builder.AppendLine("<html lang=\"en\">");
				builder.AppendLine("<head>");
					builder.AppendLine("<meta charset=\"UTF-8\">");
					builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
					builder.AppendLine($"<title>{name}</title>");
					builder.AppendLine(styling);
				builder.AppendLine("</head>");
				builder.AppendLine("<body>");
					builder.AppendLine(content);
				builder.AppendLine("</body>");
				builder.AppendLine("</html>");
				
				File.WriteAllText(outputFile, builder.ToString());
			}
		}

		static string GetContent(string sourceFolder, string sourceFile)
		{
			var builder = new StringBuilder();
			var files = Directory.GetFiles(sourceFolder, sourceFile);
			var articleLevel = 0;

			foreach (var filePath in files)
			{
				var lines = File.ReadAllLines(filePath);
				for(int i = 0; i < lines.Length; i++)
				{
					var trimmed = lines[i].Trim();
					if(trimmed.Length > 0)
					{
						// Header (#)
						if (trimmed.StartsWith("#"))
						{
							var headerSize = 1;
							for(int n = 1; n < trimmed.Length; n++)
							{
								if (trimmed[n] == '#')
								{
									headerSize++;
									continue;
								}

								while(headerSize <= articleLevel)
								{
									builder.AppendLine("</article>");
									articleLevel--;
								}
								articleLevel = headerSize;

								builder.AppendLine($"<article class=\"a{articleLevel}\">");
								trimmed = trimmed.Substring(n).TrimStart();
								builder.AppendLine($"<h{headerSize}>{ParseText(trimmed)}</h{headerSize}>");
								break;
							}
						}

						// Table
						else if(trimmed.StartsWith("|") && trimmed.EndsWith("|"))
						{
							builder.AppendLine("<table>");
							var rows = new List<string[]>();
							for(int n = i; n < lines.Length; n++)
							{
								trimmed = lines[n].Trim();
								if (!trimmed.StartsWith('|'))
								{
									i = n - 1;
									break;
								}
								rows.Add(trimmed.Substring(1, trimmed.Length-2).Trim().Split('|').Select(x => ParseText(x.Trim())).ToArray());
							}

							var alignments = rows.Count >= 2 && rows[1].All(x => x.Contains("---")) ? rows[1] : null;
							var hasHeaders = alignments?.Length > 0;

							if (hasHeaders)
							{
								builder.Append("<tr>");
								foreach(var header in rows[0])
									builder.Append($"<th>{header}</th>");
								builder.AppendLine("</tr>");
							}

							for(int n = hasHeaders ? 2 : 0; n < rows.Count; n++)
							{
								builder.Append("<tr>");
								foreach (var cell in rows[n])
									builder.Append($"<td>{cell}</td>");
								builder.AppendLine("</tr>");
							}

							builder.AppendLine("</table>");
						}

						// Blockquote (>)
						else if (trimmed.StartsWith(">"))
						{
							var allContent = new List<string>();
							for(int n = i; i < lines.Length; n++)
							{
								trimmed = lines[n].Trim();
								if (!trimmed.StartsWith(">"))
									break;

								trimmed = trimmed.Substring(1).TrimStart();
								if(trimmed.Length > 0)
									allContent.Add(ParseText(trimmed));
								i = n;
							}
							builder.AppendLine($"<blockquote><p>{string.Join("</p><p>", allContent)}</p></blockquote>");
						}

						// Page breaks
						else if (trimmed.StartsWith("---"))
						{
							builder.AppendLine("<hr style=\"page-break-after: always;\">");
						}

						// Normal text
						else
						{
							if(Translations.ShouldInclude(trimmed, out trimmed))
								builder.AppendLine($"<p>{ParseText(trimmed)}</p>");
						}
					}
				}
			}
			while (articleLevel > 0)
			{
				builder.AppendLine("</article>");
				articleLevel--;
			}
			return builder.ToString();
		}

		static string ParseText(string text)
		{
			text = Translations.Apply(text);

			var output = new StringBuilder();
			for(int i = 0; i < text.Length; i++)
			{
				switch (text[i])
				{
					case '\\':
						i++;
						if (i >= text.Length)
							continue;

						switch (text[i])
						{
							// Just ensure that we write the actual character
							case '"':
							case '\'':
							case '{':
							case '}':
							case '(':
							case ')':
							case '[':
							case ']':
							case '>':
							case '<':
							case '`':
							case '\\': output.Append(text[i]); break;

							default:
								output.Append(CreateError($"\\{text[i]}"));
								break;
						}
						break;

					case '"':
						output.Append("<span class=\"quote\">\"");
						output.Append(GetSection(text, ref i, '"'));
						output.Append("\"</span>");
						break;

					case '`':
						output.Append("<span class=\"code\">");
						output.Append(GetSection(text, ref i, '`'));
						output.Append("</span>");
						break;

					case '*':
						i++;
						if (i >= text.Length)
						{
							output.Append(text[i - 1]);
						}
						else if (text[i] == '*')
						{
							i++;
							output.Append("<b>");
							output.Append(GetSection(text, ref i, "**"));
							output.Append("</b>");
						}
						else
						{
							output.Append("<i>");
							output.Append(GetSection(text, ref i, "*"));
							output.Append("</i>");
						}
						break;

					default:
						output.Append(text[i]);
						break;
				}
			}
			return output.ToString();
		}


		static string GetSection(string text, ref int i, char end)
		{
			var output = new StringBuilder();
			for (i++; i < text.Length; i++)
			{
				if (text[i] == end && text[i - 1] != '\\')
					break;
				output.Append(text[i]);
			}
			return output.ToString();
		}

		static string GetSection(string text, ref int i, string end)
		{
			var endIndex = i;
			while (endIndex > -1)
			{
				endIndex = text.IndexOf(end, endIndex);
				if (endIndex < 0)
					return text.Substring(i);

				if (text[endIndex - 1] == '\\')
				{
					endIndex += end.Length;
					continue;
				}

				var content = text.Substring(i, endIndex - i);
				i = endIndex + end.Length - 1;
				return content;
			}

			return text.Substring(i);
		}


		static string StripCSS(string file)
		{
			var raw = File.ReadAllText(file);
			var output = new StringBuilder();

			var shouldAppendWhiteSpace = false;
			var nextMax = raw.Length - 1;
			for(int i = 0; i < raw.Length; i++)
			{
				if (char.IsWhiteSpace(raw[i]) || raw[i] == '\n' || raw[i] == '\r')
				{
					if (shouldAppendWhiteSpace)
					{
						shouldAppendWhiteSpace = false;
						output.Append(' ');
					}
					continue;
				}

				// Strip comments
				if (i < nextMax && raw[i] == '/' && raw[i+1] == '*')
				{
					var endIndex = raw.IndexOf("*/", i + 1);
					if (endIndex < 0)
						break;
					i = endIndex + 1;
					continue;
				}

				// Add text as is
				shouldAppendWhiteSpace = char.IsLetterOrDigit(raw[i]) || raw[i] == '_';
				if (raw[i] == '}') output.AppendLine(raw[i].ToString());
				else output.Append(raw[i]);
			}

			return output.ToString();
		}

		static bool TryGetPath(ref int i, string[] args, ref string value) => TryGetValue(ref i, args, ref value, "Expected a path after " + args[i]);

		static bool TryGetValue(ref int i, string[] args, ref string value, string error)
		{
			if (value != string.Empty)
			{
				Error($"Already set {args[i]}");
				return false;
			}

			i++;
			if (i >= args.Length)
			{
				Error(error);
				return false;
			}

			value = args[i];
			return true;
		}


		static void Error(string error)
		{
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine(error);
			Console.ResetColor();
		}
	}
}
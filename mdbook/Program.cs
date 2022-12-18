using System.Diagnostics;
using System.Reflection;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

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

				for(var i = 0; i < args.Length; i++)
				{
					switch (args[i])
					{
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
				if(string.IsNullOrWhiteSpace(sourceFile) || (!Directory.Exists(sourceFile) && !File.Exists(sourceFile)))
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
					? $"<style>{StripCSS(styleFile)}</style>"
					: ""
					;

				var content = GetContent(sourceFolder, sourceFile);

				var name = "Test";

				var builder = new StringBuilder();
				builder.Append("<!DOCTYPE html>");
				builder.Append("<html lang=\"en\">");
				builder.Append("<head>");
					builder.Append("<meta charset=\"UTF-8\">");
					builder.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
					builder.Append($"<title>{name}</title>");
					builder.Append(styling);
				builder.Append("</head>");
				builder.Append("<body>");
					builder.Append(content);
				builder.Append("</body>");
				builder.Append("</html>");
				
				File.WriteAllText(outputFile, builder.ToString());
			}
		}

		static string GetContent(string sourceFolder, string sourceFile)
		{
			var builder = new StringBuilder();
			var files = Directory.GetFiles(sourceFolder, sourceFile);
			
			foreach(var filePath in files)
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

								trimmed = trimmed.Substring(n).TrimStart();
								builder.AppendLine($"<h{headerSize}>{ParseText(trimmed)}</h{headerSize}>");
								break;
							}
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
							builder.AppendLine("<p style=\"page-break-after: always;\"></p>");
						}

						// Normal text
						else
						{
							builder.AppendLine($"<p>{ParseText(trimmed)}</p>");
						}
					}
				}
			}
			return builder.ToString();
		}

		static string ParseText(string text)
		{
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
							output.Append(GetSection(text, ref i, "**"));
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



		static string CreateError(string e) => $"<span class=\"error\">Unexpected character: {e}</span>";

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
				output.Append(raw[i]);
			}

			return output.ToString();
		}

		static bool TryGetPath(ref int i, string[] args, ref string value)
		{
			if(value != string.Empty)
			{
				Error($"Already set {args[i]}");
				return false;
			}

			i++;
			if (i >= args.Length)
			{
				Error("Expected a path after " + args[i-1]);
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
﻿using System.Diagnostics;
using System.Reflection;
using System.Text;

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
			var files = Directory.GetFiles(sourceFolder, sourceFile);

			return "<p>Test</p>";
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
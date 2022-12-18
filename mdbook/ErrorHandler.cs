using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mdbook
{
	public static class ErrorHandler
	{
		public static string CreateError(string e) => $"<span class=\"error\">Unexpected character: {e}</span>";
	}
}

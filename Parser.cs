
using System;
using System.IO;
using System.Text;

namespace Boolio {

public class Parser {
	public const int _EOF = 0;
	public const int _bool = 1;
	public const int _variable = 2;
	public const int maxT = 13;

	const bool T = true;
	const bool x = false;
	const int minErrDist = 2;

	public static Token token;    // last recognized token   /* pdt */
	public static Token la;       // lookahead token
	static int errDist = minErrDist;

	

	static void SynErr (int n) {
		if (errDist >= minErrDist) Errors.SynErr(la.line, la.col, n);
		errDist = 0;
	}

	public static void SemErr (string msg) {
		if (errDist >= minErrDist) Errors.Error(token.line, token.col, msg); /* pdt */
		errDist = 0;
	}

	public static void SemError (string msg) {
		if (errDist >= minErrDist) Errors.Error(token.line, token.col, msg); /* pdt */
		errDist = 0;
	}

	public static void Warning (string msg) { /* pdt */
		if (errDist >= minErrDist) Errors.Warn(token.line, token.col, msg);
		errDist = 2; //++ 2009/11/04
	}

	public static bool Successful() { /* pdt */
		return Errors.count == 0;
	}

	public static string LexString() { /* pdt */
		return token.val;
	}

	public static string LookAheadString() { /* pdt */
		return la.val;
	}

	static void Get () {
		for (;;) {
			token = la; /* pdt */
			la = Scanner.Scan();
			if (la.kind <= maxT) { ++errDist; break; }

			la = token; /* pdt */
		}
	}

	static void Expect (int n) {
		if (la.kind==n) Get(); else { SynErr(n); }
	}

	static bool StartOf (int s) {
		return set[s, la.kind];
	}

	static void ExpectWeak (int n, int follow) {
		if (la.kind == n) Get();
		else {
			SynErr(n);
			while (!StartOf(follow)) Get();
		}
	}

	static bool WeakSeparator (int n, int syFol, int repFol) {
		bool[] s = new bool[maxT+1];
		if (la.kind == n) { Get(); return true; }
		else if (StartOf(repFol)) return false;
		else {
			for (int i=0; i <= maxT; i++) {
				s[i] = set[syFol, i] || set[repFol, i] || set[0, i];
			}
			SynErr(n);
			while (!s[la.kind]) Get();
			return StartOf(syFol);
		}
	}

	static void Boolio() {
		while (la.kind == 2) {
			Statement();
		}
		Expect(0);
	}

	static void Statement() {
		Expect(2);
		Expect(3);
		Expression();
		Expect(4);
	}

	static void Expression() {
		Single();
		while (StartOf(1)) {
			Double();
		}
	}

	static void Single() {
		if (la.kind == 1) {
			Get();
		} else if (la.kind == 2) {
			Get();
		} else if (la.kind == 5) {
			Parentheses();
		} else if (la.kind == 7 || la.kind == 8) {
			Not();
		} else SynErr(14);
	}

	static void Double() {
		if (la.kind == 11 || la.kind == 12) {
			And();
		} else if (la.kind == 9 || la.kind == 10) {
			Or();
		} else SynErr(15);
	}

	static void Parentheses() {
		Expect(5);
		Expression();
		while (StartOf(2)) {
			Expression();
		}
		Expect(6);
	}

	static void And() {
		if (la.kind == 11) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(16);
		} else if (la.kind == 12) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(17);
		} else SynErr(18);
	}

	static void Or() {
		if (la.kind == 9) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(19);
		} else if (la.kind == 10) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(20);
		} else SynErr(21);
	}

	static void Not() {
		if (la.kind == 7) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(22);
		} else if (la.kind == 8) {
			Get();
			if (StartOf(2)) {
				Expression();
			} else if (la.kind == 5) {
				Parentheses();
			} else SynErr(23);
		} else SynErr(24);
	}



	public static void Parse() {
		la = new Token();
		la.val = "";
		Get();
		Boolio();
		Expect(0);

	}

	static bool[,] set = {
		{T,x,x,x, x,x,x,x, x,x,x,x, x,x,x},
		{x,x,x,x, x,x,x,x, x,T,T,T, T,x,x},
		{x,T,T,x, x,T,x,T, T,x,x,x, x,x,x}

	};

} // end Parser

/* pdt - considerable extension from here on */

public class ErrorRec {
	public int line, col, num;
	public string str;
	public ErrorRec next;

	public ErrorRec(int l, int c, string s) {
		line = l; col = c; str = s; next = null;
	}

} // end ErrorRec

public class Errors {

	public static int count = 0;                                     // number of errors detected
	public static int warns = 0;                                     // number of warnings detected
	public static string errMsgFormat = "file {0} : ({1}, {2}) {3}"; // 0=file 1=line, 2=column, 3=text
	static string fileName = "";
	static string listName = "";
	static bool mergeErrors = false;
	static StreamWriter mergedList;

	static ErrorRec first = null, last;
	static bool eof = false;

	static string GetLine() {
		char ch, CR = '\r', LF = '\n';
		int l = 0;
		StringBuilder s = new StringBuilder();
		ch = (char) Buffer.Read();
		while (ch != Buffer.EOF && ch != CR && ch != LF) {
			s.Append(ch); l++; ch = (char) Buffer.Read();
		}
		eof = (l == 0 && ch == Buffer.EOF);
		if (ch == CR) {  // check for MS-DOS
			ch = (char) Buffer.Read();
			if (ch != LF && ch != Buffer.EOF) Buffer.Pos--;
		}
		return s.ToString();
	}

	static void Display (string s, ErrorRec e) {
		mergedList.Write("**** ");
		for (int c = 1; c < e.col; c++)
			if (s[c-1] == '\t') mergedList.Write("\t"); else mergedList.Write(" ");
		mergedList.WriteLine("^ " + e.str);
	}

	public static void Init (string fn, string dir, bool merge) {
		fileName = fn;
		listName = dir + "listing.txt";
		mergeErrors = merge;
		if (mergeErrors)
			try {
				mergedList = new StreamWriter(new FileStream(listName, FileMode.Create));
			} catch (IOException) {
				Errors.Exception("-- could not open " + listName);
			}
	}

	public static void Summarize () {
		if (mergeErrors) {
			mergedList.WriteLine();
			ErrorRec cur = first;
			Buffer.Pos = 0;
			int lnr = 1;
			string s = GetLine();
			while (!eof) {
				mergedList.WriteLine("{0,4} {1}", lnr, s);
				while (cur != null && cur.line == lnr) {
					Display(s, cur); cur = cur.next;
				}
				lnr++; s = GetLine();
			}
			if (cur != null) {
				mergedList.WriteLine("{0,4}", lnr);
				while (cur != null) {
					Display(s, cur); cur = cur.next;
				}
			}
			mergedList.WriteLine();
			mergedList.WriteLine(count + " errors detected");
			if (warns > 0) mergedList.WriteLine(warns + " warnings detected");
			mergedList.Close();
		}
		switch (count) {
			case 0 : Console.WriteLine("Parsed correctly"); break;
			case 1 : Console.WriteLine("1 error detected"); break;
			default: Console.WriteLine(count + " errors detected"); break;
		}
		if (warns > 0) Console.WriteLine(warns + " warnings detected");
		if ((count > 0 || warns > 0) && mergeErrors) Console.WriteLine("see " + listName);
	}

	public static void StoreError (int line, int col, string s) {
		if (mergeErrors) {
			ErrorRec latest = new ErrorRec(line, col, s);
			if (first == null) first = latest; else last.next = latest;
			last = latest;
		} else Console.WriteLine(errMsgFormat, fileName, line, col, s);
	}

	public static void SynErr (int line, int col, int n) {
		string s;
		switch (n) {
			case 0: s = "EOF expected"; break;
			case 1: s = "bool expected"; break;
			case 2: s = "variable expected"; break;
			case 3: s = "\"=\" expected"; break;
			case 4: s = "\";\" expected"; break;
			case 5: s = "\"(\" expected"; break;
			case 6: s = "\")\" expected"; break;
			case 7: s = "\"!\" expected"; break;
			case 8: s = "\"not\" expected"; break;
			case 9: s = "\"||\" expected"; break;
			case 10: s = "\"or\" expected"; break;
			case 11: s = "\"&&\" expected"; break;
			case 12: s = "\"and\" expected"; break;
			case 13: s = "??? expected"; break;
			case 14: s = "invalid Single"; break;
			case 15: s = "invalid Double"; break;
			case 16: s = "invalid And"; break;
			case 17: s = "invalid And"; break;
			case 18: s = "invalid And"; break;
			case 19: s = "invalid Or"; break;
			case 20: s = "invalid Or"; break;
			case 21: s = "invalid Or"; break;
			case 22: s = "invalid Not"; break;
			case 23: s = "invalid Not"; break;
			case 24: s = "invalid Not"; break;

			default: s = "error " + n; break;
		}
		StoreError(line, col, s);
		count++;
	}

	public static void SemErr (int line, int col, int n) {
		StoreError(line, col, ("error " + n));
		count++;
	}

	public static void Error (int line, int col, string s) {
		StoreError(line, col, s);
		count++;
	}

	public static void Error (string s) {
		if (mergeErrors) mergedList.WriteLine(s); else Console.WriteLine(s);
		count++;
	}

	public static void Warn (int line, int col, string s) {
		StoreError(line, col, s);
		warns++;
	}

	public static void Warn (string s) {
		if (mergeErrors) mergedList.WriteLine(s); else Console.WriteLine(s);
		warns++;
	}

	public static void Exception (string s) {
		Console.WriteLine(s);
		System.Environment.Exit(1);
	}

} // end Errors

} // end namespace

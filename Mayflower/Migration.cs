using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Mayflower
{
    enum MigrateMode
    {
        Skip,
        Run,
        Rename,
        HashMismatch,
    }

    public class Migration
    {
        static readonly MD5CryptoServiceProvider s_md5Provider = new MD5CryptoServiceProvider();
        static readonly Regex s_lineEndings = new Regex("\r\n|\n\r|\n|\r", RegexOptions.Compiled);
        static readonly Regex s_reference = new Regex("^\\s*:r\\s*(.*)\\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        public List<string> SqlCommands { get; }
        public string Hash { get; }
        public string Filename { get; }
        public bool UseTransaction { get; }

        const string NoTransactionToken = "-- no transaction --";

        /// <summary>
        /// The included filepath. The included file has to be in the migration''s folder or below.
        /// The content of the current migration will be replaced by the content of the included file.
        /// </summary>
        public string IncludedFilepath { get; }

        internal Migration(string filePath, Regex commandSplitter)
        {
            var sql = File.ReadAllText(filePath, Encoding.GetEncoding("iso-8859-1"));
            SqlCommands = commandSplitter.Split(sql).Where(s => s.Trim().Length > 0).ToList();
            Hash = GetHash(sql);
            Filename = Path.GetFileName(filePath);

            UseTransaction = !sql.StartsWith(NoTransactionToken);

            // check if the migration includes another file
            IList<string> includedRelativeFilepaths = null;
            try
            {
                includedRelativeFilepaths = GetRelativeIncludedFilepaths(sql);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing the migration '{Filename}'", ex);
            }

            if (includedRelativeFilepaths.Count > 0)
            {
                SqlCommands.Clear();
                var fileDirectory = Path.GetFullPath(Path.GetDirectoryName(filePath));

                foreach (var includedRelativeFilepath in includedRelativeFilepaths)
                {
                    IncludedFilepath = Path.GetFullPath(Path.Combine(fileDirectory, includedRelativeFilepath));
                    // check if the included file is under the migration's folder
                    if (!CheckIncludedFilepath(fileDirectory, IncludedFilepath))
                    {
                        throw new Exception(
                            $"The migration '{Filename}' tries to include a file that is not relative to the migration's " +
                            $"folder: '{IncludedFilepath}'");
                    }

                    // read the included file
                    if (File.Exists(IncludedFilepath))
                    {
                        sql = File.ReadAllText(IncludedFilepath, Encoding.GetEncoding("iso-8859-1"));
                        SqlCommands.AddRange(commandSplitter.Split(sql).Where(s => s.Trim().Length > 0));
                    }
                    else
                    {
                        throw new Exception(
                            $"The migration '{Filename}' tries to include a file that does not exist: {IncludedFilepath}");
                    }
                }
            }
        }

        internal MigrateMode GetMigrateMode(AlreadyRan alreadyRan)
        {
            MigrationRow row;
            if (alreadyRan.ByFilename.TryGetValue(Filename, out row))
            {
                return row.Hash == Hash ? MigrateMode.Skip : MigrateMode.HashMismatch;
            }

            if (alreadyRan.ByHash.TryGetValue(Hash, out row))
            {
                return MigrateMode.Rename;
            }

            return MigrateMode.Run;
        }

        static string GetHash(string str)
        {
            var normalized = NormalizeLineEndings(str);
            var inputBytes = Encoding.Unicode.GetBytes(normalized);

            byte[] hashBytes;
            lock (s_md5Provider)
            {
                hashBytes = s_md5Provider.ComputeHash(inputBytes);
            }

            return new Guid(hashBytes).ToString();
        }

        static string NormalizeLineEndings(string str)
        {
            return s_lineEndings.Replace(str, "\n");
        }

        /// <summary>
        /// Returns the path of the included files.
        /// The paths are expected to be after the string ":r" (line the :r command from sqlcmd)
        /// ":r" is expected to be at the begining of the file or after the special string "-- no transaction --"
        /// and then at the beginning of each line
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public static IList<string> GetRelativeIncludedFilepaths(string sql)
        {
            var result = new List<string>();

            sql = sql.Replace(NoTransactionToken, "");
            sql = sql.Trim();
            if (!sql.StartsWith(":r"))
            {
                // if the migration content does not start with :r, then it is a normal migration
                return result;
            }

            // all the lines should start with :r or contain only white space or comment
            var lines = sql.Split('\n');
            foreach (var l in lines)
            {
                var line = l.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("--"))
                {
                    // empty line / comment, check next line
                    continue;;
                }

                if (line.StartsWith(":r"))
                {
                    // the text after ":r" is the path to a file to include
                    result.Add(line.Substring(2).Trim());
                }
                else
                {
                    throw new Exception("Invalid migration: if a migration includes files using the command :r then all the lines should " +
                                        "either include files, contain empty spaces or comments");
                }
            }

            return result;
        }

        /// <summary>
        /// Check if a given filepath is under a given folder
        /// </summary>
        /// <param name="baseFolder"></param>
        /// <param name="includedFilepath"></param>
        /// <returns></returns>
        static bool CheckIncludedFilepath(string baseFolder, string includedFilepath)
        {
            baseFolder = Path.GetFullPath(baseFolder);
            includedFilepath = Path.GetFullPath(includedFilepath);

            if (includedFilepath.StartsWith(baseFolder))
            {
                return true;
            }

            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Mayflower.Tests
{
    [TestFixture]
    public class MigrationTests
    {
        [Test]
        public void GetRelativeIncludedFilepathsTest_StandardMigration()
        {
            var sql =
@"DROP PROCEDURE if exists [dbo].[proc1]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE PROCEDURE [dbo].[proc1]
	@Param1 bigint
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT @Param1
END
GO
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>();
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_OneIncludeAndSomeEmptyLinesAndComments()
        {
            var sql =
@":r procedures\proc1.sql

-- the command :r is at the beginning of the file. The migration should be successful.
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_OneIncludeAndSomeEmptyLinesAndCommentsAndNoTransactionPrefix()
        {
            var sql =
@"-- no transaction --
:r procedures\proc1.sql

-- The command :r is after the special expression -- no transaction --
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_TheIncludeCommandIsAfterSpaces()
        {
            var sql =
@"
    :r procedures\proc1.sql

-- The command :r is after spaces";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_NonSpaceCharactersBeforeTheIncludeCommand()
        {
            var sql =
@"select 1

:r procedures\proc1.sql

-- There are non space characters before command :r
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>();
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_MultipleIncludesAndSomeEmptyLinesAndComments()
        {
            var sql =
@":r procedures\proc1.sql
:r procedures\proc1.sql
-- only lines with :r or comments. The migration should be successful.
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql",
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_MultipleIncludesAndSomeEmptyLinesAndCommentsAndNoTransactionPrefix()
        {
            var sql =
@"-- no transaction --
:r procedures\proc1.sql
:r procedures\proc1.sql

-- The command :r is after the special expression -- no transaction --
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql",
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_MultipleIncludesAndSomeEmptyLinesAndCommentsBetweenIncludes()
        {
            var sql =
                @"-- no transaction --
:r procedures\proc1.sql
-- comment

:r procedures\proc1.sql
-- comment
";

            var actual = Migration.GetRelativeIncludedFilepaths(sql);
            var expected = new List<string>
            {
                @"procedures\proc1.sql",
                @"procedures\proc1.sql"
            };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetRelativeIncludedFilepathsTest_MultipleIncludesInvalidMigration()
        {
            var sql =
                @"-- no transaction --
:r procedures\proc1.sql

select 1

:r procedures\proc1.sql

";

            Assert.Throws<Exception>(delegate { Migration.GetRelativeIncludedFilepaths(sql); });
        }

    }
}

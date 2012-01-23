using System.Data;
using Migrator.Framework;

namespace NzbDrone.Core.Datastore.Migrations
{

    [Migration(20120123)]
    public class Migration20120123 : NzbDroneMigration
    {
        protected override void MainDbUpgrade()
        {
            Database.ExecuteNonQuery("ALTER TABLE SERIES ALTER COLUMN OVERVIEW NTEXT");
            Database.ExecuteNonQuery("ALTER TABLE EPISODES ALTER COLUMN OVERVIEW NTEXT");
        }

        protected override void LogDbUpgrade()
        {
            Database.ExecuteNonQuery("ALTER TABLE LOGS ALTER COLUMN EXCEPTION NTEXT");
        }
    }
}
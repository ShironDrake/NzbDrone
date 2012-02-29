using System;
using System.Data;
using Migrator.Framework;

namespace NzbDrone.Core.Datastore.Migrations
{

    [Migration(20120229)]
    public class Migration20120229 : NzbDroneMigration
    {
        protected override void MainDbUpgrade()
        {
            Database.AddColumn("Series", "AbsoluteNumbering", DbType.Boolean, ColumnProperty.None);
            Database.AddColumn("Episodes", "AbsoluteNumber", DbType.Int32, ColumnProperty.None);
        }
    }
}

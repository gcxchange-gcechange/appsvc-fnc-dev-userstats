using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace appsvc_fnc_dev_userstats
{
    public class usersData
    {
        public string Id { get; set; }
        public System.DateTimeOffset? creationDate { get; set; }
        public string mail { get; set; }
    }
    public class groupsData
    {
        public string displayName;
        public int countMember;
        public string groupId;
        public string creationDate;
        public string description;
        public IEnumerable<string> groupType;
        public List<string> userlist;
    }
    public class activeuserData
    {
        public string userid;
        public string UserDisplayName;
    }
    public class countactiveuserData
    {
        public string name;
        public int countActiveusers;
    }

}

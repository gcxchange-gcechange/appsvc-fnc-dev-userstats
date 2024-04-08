using System;
using System.Collections.Generic;

namespace appsvc_fnc_dev_userstats
{
    public class usersData
    {
        public string Id { get; set; }
        public DateTimeOffset? creationDate { get; set; }
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
        public List<ActiveUserCountByDomain> countByDomain;
    }

    public class ActiveUserCountByDomain
    {
        public string domain;
        public int count;

        public ActiveUserCountByDomain(string domainName, int userCount) {
            domain = domainName;
            count = userCount;
        }
    }

    public class SingleGroup
    {
        public string displayName;
        public int countMember;
        public string groupId;
        public string creationDate;
        public string description;
        public IEnumerable<string> groupType;
        public List<String> userlist;

        public SingleGroup(string displayName, int countMember, string groupId, string creationDate, string description, IEnumerable<string> groupType, List<String> userlist)
        {
            this.displayName = displayName;
            this.countMember = countMember;
            this.groupId = groupId;
            this.creationDate = creationDate;
            this.description = description;
            this.groupType = groupType;
            this.userlist = userlist;
        }
    }
}
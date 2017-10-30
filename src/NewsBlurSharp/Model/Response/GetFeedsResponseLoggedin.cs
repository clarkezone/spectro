using System;
using System.Collections.Generic;
using System.Text;

namespace NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn
{

    public class Rootobject
    {
        public object[] folders { get; set; }
        public object[] saved_searches { get; set; }
        public int user_id { get; set; }
        public Social_Profile social_profile { get; set; }
        public User_Profile user_profile { get; set; }
        public Starred_Counts[] starred_counts { get; set; }
        public int starred_count { get; set; }
        public bool is_staff { get; set; }
        public string result { get; set; }
        public bool authenticated { get; set; }
        public Feeds feeds { get; set; }
        public Social_Services social_services { get; set; }
        public object categories { get; set; }
        public Social_Feeds[] social_feeds { get; set; }
    }

    public class Social_Profile
    {
        public string website { get; set; }
        public int[] following_user_ids { get; set; }
        public int following_count { get; set; }
        public int shared_stories_count { get; set; }
        public bool _private { get; set; }
        public string large_photo_url { get; set; }
        public string id { get; set; }
        public string feed_address { get; set; }
        public int user_id { get; set; }
        public string feed_link { get; set; }
        public int[] follower_user_ids { get; set; }
        public string location { get; set; }
        public object popular_publishers { get; set; }
        public int follower_count { get; set; }
        public string username { get; set; }
        public string bio { get; set; }
        public int average_stories_per_month { get; set; }
        public string feed_title { get; set; }
        public string photo_service { get; set; }
        public int stories_last_month { get; set; }
        public string photo_url { get; set; }
        public int num_subscribers { get; set; }
        public bool _protected { get; set; }
    }

    public class User_Profile
    {
        public bool hide_getting_started { get; set; }
        public Preferences preferences { get; set; }
        public bool tutorial_finished { get; set; }
        public bool has_setup_feeds { get; set; }
        public bool is_premium { get; set; }
        public string dashboard_date { get; set; }
        public bool has_trained_intelligence { get; set; }
        public bool has_found_friends { get; set; }
    }

    public class Preferences
    {
        public string hide_read_feeds { get; set; }
        public string intro_page { get; set; }
        public string story_titles_pane_size { get; set; }
    }

    public class Feeds
    {
        public Feed[] FeedItems { get; set; }
        //public _783451 _783451 { get; set; }
        //public _2235770 _2235770 { get; set; }
        //public _1546193 _1546193 { get; set; }
        //public _5824384 _5824384 { get; set; }
        //public _55502 _55502 { get; set; }
        //public _2558658 _2558658 { get; set; }
        //public _2558604 _2558604 { get; set; }
        //public _1474750 _1474750 { get; set; }
        //public _238996 _238996 { get; set; }
        //public _514556 _514556 { get; set; }
        //public _2558568 _2558568 { get; set; }
        //public _2558640 _2558640 { get; set; }
        //public _2558643 _2558643 { get; set; }
        //public _302600 _302600 { get; set; }
        //public _2558645 _2558645 { get; set; }
        //public _75151 _75151 { get; set; }
        //public _2558646 _2558646 { get; set; }
        //public _2558649 _2558649 { get; set; }
        //public _1490976 _1490976 { get; set; }
        //public _132216 _132216 { get; set; }
        //public _1345537 _1345537 { get; set; }
        //public _1446074 _1446074 { get; set; }
        //public _2558567 _2558567 { get; set; }
        //public _76772 _76772 { get; set; }
        //public _621393 _621393 { get; set; }
        //public _667569 _667569 { get; set; }
        //public _34372 _34372 { get; set; }
        //public _1628687 _1628687 { get; set; }
        //public _667094 _667094 { get; set; }
        //public _19232 _19232 { get; set; }
        //public _146359 _146359 { get; set; }
        //public _257423 _257423 { get; set; }
        //public _91366 _91366 { get; set; }
        //public _517295 _517295 { get; set; }
        //public _11461 _11461 { get; set; }
        //public _6844 _6844 { get; set; }
        //public _73088 _73088 { get; set; }
        //public _1534404 _1534404 { get; set; }
        //public _105188 _105188 { get; set; }
        //public _882342 _882342 { get; set; }
        //public _107696 _107696 { get; set; }
        //public _16678 _16678 { get; set; }
        //public _6384613 _6384613 { get; set; }
        //public _1796 _1796 { get; set; }
        //public _839013 _839013 { get; set; }
        //public _1794 _1794 { get; set; }
        //public _6575217 _6575217 { get; set; }
        //public _5904181 _5904181 { get; set; }
        //public _6705932 _6705932 { get; set; }
        //public _6808591 _6808591 { get; set; }
        //public _2558659 _2558659 { get; set; }
        //public _1495031 _1495031 { get; set; }
        //public _810049 _810049 { get; set; }
        //public _2558652 _2558652 { get; set; }
        //public _2558653 _2558653 { get; set; }
        //public _2558650 _2558650 { get; set; }
        //public _2558651 _2558651 { get; set; }
        //public _6175064 _6175064 { get; set; }
        //public _76580 _76580 { get; set; }
        //public _2548534 _2548534 { get; set; }
        //public _868670 _868670 { get; set; }
        //public _6247838 _6247838 { get; set; }
        //public _17735 _17735 { get; set; }
        //public _71947 _71947 { get; set; }
        //public _1167900 _1167900 { get; set; }
        //public _1996198 _1996198 { get; set; }
        //public _2558642 _2558642 { get; set; }
        //public _1289252 _1289252 { get; set; }
        //public _46424 _46424 { get; set; }
        //public _19148 _19148 { get; set; }
        //public _1223789 _1223789 { get; set; }
        //public _1284941 _1284941 { get; set; }
        //public _258928 _258928 { get; set; }
        //public _189669 _189669 { get; set; }
        //public _202914 _202914 { get; set; }
        //public _374 _374 { get; set; }
        //public _236995 _236995 { get; set; }
        //public _6270412 _6270412 { get; set; }
        //public _1435345 _1435345 { get; set; }
        //public _12665 _12665 { get; set; }
        //public _2558647 _2558647 { get; set; }
        //public _1027928 _1027928 { get; set; }
        //public _19201 _19201 { get; set; }
        //public _191942 _191942 { get; set; }
        //public _99278 _99278 { get; set; }
        //public _297770 _297770 { get; set; }
        //public _11568 _11568 { get; set; }
        //public _2558629 _2558629 { get; set; }
        //public _758776 _758776 { get; set; }
        //public _5721214 _5721214 { get; set; }
        //public _2558623 _2558623 { get; set; }
        //public _764215 _764215 { get; set; }
        //public _2558621 _2558621 { get; set; }
        //public _544229 _544229 { get; set; }
        //public _2558626 _2558626 { get; set; }
        //public _1531437 _1531437 { get; set; }
        //public _14232 _14232 { get; set; }
        //public _16566 _16566 { get; set; }
        //public _5842375 _5842375 { get; set; }
        //public _6265 _6265 { get; set; }
        //public _7243 _7243 { get; set; }
        //public _5535592 _5535592 { get; set; }
        //public _7241 _7241 { get; set; }
        //public _191954 _191954 { get; set; }
        //public _193275 _193275 { get; set; }
        //public _59302 _59302 { get; set; }
        //public _1709676 _1709676 { get; set; }
        //public _138194 _138194 { get; set; }
        //public _169882 _169882 { get; set; }
        //public _19303 _19303 { get; set; }
        //public _232373 _232373 { get; set; }
        //public _98002 _98002 { get; set; }
        //public _253924 _253924 { get; set; }
        //public _13221 _13221 { get; set; }
        //public _78432 _78432 { get; set; }
        //public _699727 _699727 { get; set; }
        //public _5966297 _5966297 { get; set; }
        //public _2558636 _2558636 { get; set; }
        //public _854006 _854006 { get; set; }
        //public _6320835 _6320835 { get; set; }
        //public _2073974 _2073974 { get; set; }
        //public _2446 _2446 { get; set; }
        //public _772681 _772681 { get; set; }
        //public _17402 _17402 { get; set; }
        //public _297775 _297775 { get; set; }
        //public _17360 _17360 { get; set; }
        //public _1358 _1358 { get; set; }
        //public _16056 _16056 { get; set; }
        //public _1663556 _1663556 { get; set; }
        //public _514740 _514740 { get; set; }
        //public _4191 _4191 { get; set; }
        //public _2558639 _2558639 { get; set; }
        //public _2145601 _2145601 { get; set; }
        //public _5673 _5673 { get; set; }
        //public _1106432 _1106432 { get; set; }
        //public _1935304 _1935304 { get; set; }
        //public _609602 _609602 { get; set; }
        //public _2558616 _2558616 { get; set; }
        //public _101 _101 { get; set; }
        //public _2558634 _2558634 { get; set; }
        //public _13050 _13050 { get; set; }
        //public _104 _104 { get; set; }
        //public _206631 _206631 { get; set; }
        //public _5590671 _5590671 { get; set; }
        //public _1039567 _1039567 { get; set; }
        //public _91234 _91234 { get; set; }
        //public _775659 _775659 { get; set; }
        //public _6274386 _6274386 { get; set; }
        //public _430989 _430989 { get; set; }
        //public _46392 _46392 { get; set; }
        //public _6105344 _6105344 { get; set; }
        //public _417680 _417680 { get; set; }
        //public _567404 _567404 { get; set; }
        //public _47421 _47421 { get; set; }
        //public _6391055 _6391055 { get; set; }
        //public _459557 _459557 { get; set; }
        //public _93450 _93450 { get; set; }
        //public _457982 _457982 { get; set; }
        //public _1418863 _1418863 { get; set; }
        //public _12786 _12786 { get; set; }
        //public _470972 _470972 { get; set; }
        //public _211301 _211301 { get; set; }
        //public _2558605 _2558605 { get; set; }
        //public _89739 _89739 { get; set; }
        //public _2558607 _2558607 { get; set; }
        //public _331010 _331010 { get; set; }
        //public _517329 _517329 { get; set; }
        //public _2558603 _2558603 { get; set; }
        //public _16640 _16640 { get; set; }
        //public _76800 _76800 { get; set; }
        //public _1784822 _1784822 { get; set; }
        //public _343233 _343233 { get; set; }
        //public _256415 _256415 { get; set; }
        //public _666772 _666772 { get; set; }
        //public _561762 _561762 { get; set; }
        //public _6928 _6928 { get; set; }
        //public _210152 _210152 { get; set; }
        //public _666779 _666779 { get; set; }
        //public _1493688 _1493688 { get; set; }
        //public _1047811 _1047811 { get; set; }
        //public _386858 _386858 { get; set; }
        //public _71954 _71954 { get; set; }
        //public _2558633 _2558633 { get; set; }
        //public _3489469 _3489469 { get; set; }
        //public _2464371 _2464371 { get; set; }
        //public _865710 _865710 { get; set; }
        //public _4949 _4949 { get; set; }
        //public _5908530 _5908530 { get; set; }
        //public _2382981 _2382981 { get; set; }
        //public _128722 _128722 { get; set; }
        //public _567410 _567410 { get; set; }
        //public _251311 _251311 { get; set; }
        //public _1753317 _1753317 { get; set; }
        //public _766345 _766345 { get; set; }
        //public _349029 _349029 { get; set; }
        //public _46414 _46414 { get; set; }
        //public _567418 _567418 { get; set; }
        //public _76730 _76730 { get; set; }
        //public _24927 _24927 { get; set; }
        //public _1522441 _1522441 { get; set; }
        //public _2558580 _2558580 { get; set; }
        //public _2558610 _2558610 { get; set; }
        //public _2558611 _2558611 { get; set; }
        //public _1127260 _1127260 { get; set; }
        //public _198063 _198063 { get; set; }
        //public _55373 _55373 { get; set; }
        //public _2558615 _2558615 { get; set; }
        //public _2558590 _2558590 { get; set; }
        //public _46475 _46475 { get; set; }
        //public _6720165 _6720165 { get; set; }
        //public _6030810 _6030810 { get; set; }
        //public _2558597 _2558597 { get; set; }
        //public _2558596 _2558596 { get; set; }
        //public _99 _99 { get; set; }
        //public _87279 _87279 { get; set; }
        //public _398979 _398979 { get; set; }
        //public _4444 _4444 { get; set; }
        //public _816838 _816838 { get; set; }
        //public _13292 _13292 { get; set; }
        //public _6240349 _6240349 { get; set; }
        //public _1883966 _1883966 { get; set; }
        //public _31958 _31958 { get; set; }
        //public _4931 _4931 { get; set; }
        //public _2211 _2211 { get; set; }
        //public _17279 _17279 { get; set; }
        //public _3203 _3203 { get; set; }
        //public _1568815 _1568815 { get; set; }
        //public _16930 _16930 { get; set; }
        //public _306035 _306035 { get; set; }
        //public _150188 _150188 { get; set; }
        //public _6083107 _6083107 { get; set; }
        //public _7032 _7032 { get; set; }
        //public _59114 _59114 { get; set; }
        //public _3897 _3897 { get; set; }
        //public _13152 _13152 { get; set; }
        //public _2558664 _2558664 { get; set; }
        //public _19223 _19223 { get; set; }
        //public _386048 _386048 { get; set; }
        //public _2698 _2698 { get; set; }
        //public _234232 _234232 { get; set; }
        //public _1747 _1747 { get; set; }
        //public _93664 _93664 { get; set; }
        //public _589914 _589914 { get; set; }
        //public _2558612 _2558612 { get; set; }
        //public _1161 _1161 { get; set; }
        //public _105917 _105917 { get; set; }
        //public _15145 _15145 { get; set; }
        //public _6423657 _6423657 { get; set; }
        //public _257535 _257535 { get; set; }
        //public _6057890 _6057890 { get; set; }
        //public _556 _556 { get; set; }
        //public _592698 _592698 { get; set; }
        //public _242887 _242887 { get; set; }
        //public _2558667 _2558667 { get; set; }
        //public _11525 _11525 { get; set; }
        //public _155816 _155816 { get; set; }
        //public _1872897 _1872897 { get; set; }
        //public _66597 _66597 { get; set; }
        //public _666091 _666091 { get; set; }
        //public _2558660 _2558660 { get; set; }
        //public _87668 _87668 { get; set; }
        //public _5299634 _5299634 { get; set; }
        //public _211327 _211327 { get; set; }
        //public _666751 _666751 { get; set; }
        //public _2558669 _2558669 { get; set; }
        //public _2489247 _2489247 { get; set; }
        //public _13067 _13067 { get; set; }
        //public _296024 _296024 { get; set; }
        //public _103077 _103077 { get; set; }
        //public _611222 _611222 { get; set; }
        //public _6640981 _6640981 { get; set; }
        //public _138774 _138774 { get; set; }
        //public _47420 _47420 { get; set; }
        //public _589249 _589249 { get; set; }
        //public _283938 _283938 { get; set; }
        //public _2421914 _2421914 { get; set; }
        //public _349246 _349246 { get; set; }
        //public _2558625 _2558625 { get; set; }
        //public _4140124 _4140124 { get; set; }
        //public _1394487 _1394487 { get; set; }
        //public _2327512 _2327512 { get; set; }
        //public _349654 _349654 { get; set; }
        //public _1848627 _1848627 { get; set; }
        //public _664673 _664673 { get; set; }
        //public _1753370 _1753370 { get; set; }
        //public _666081 _666081 { get; set; }
        //public _6098566 _6098566 { get; set; }
        //public _46346 _46346 { get; set; }
        //public _132084 _132084 { get; set; }
        //public _17801 _17801 { get; set; }
        //public _6403592 _6403592 { get; set; }
        //public _2558675 _2558675 { get; set; }
        //public _2558676 _2558676 { get; set; }
        //public _2558677 _2558677 { get; set; }
        //public _2558670 _2558670 { get; set; }
        //public _2558671 _2558671 { get; set; }
        //public _2558672 _2558672 { get; set; }
        //public _6209238 _6209238 { get; set; }
        //public _2558571 _2558571 { get; set; }
        //public _2098078 _2098078 { get; set; }
        //public _2558577 _2558577 { get; set; }
        //public _169864 _169864 { get; set; }
        //public _649660 _649660 { get; set; }
        //public _2478635 _2478635 { get; set; }
        //public _272165 _272165 { get; set; }
        //public _115713 _115713 { get; set; }
        //public _354289 _354289 { get; set; }
        //public _1873300 _1873300 { get; set; }
        //public _6389381 _6389381 { get; set; }
        //public _132152 _132152 { get; set; }
        //public _514715 _514715 { get; set; }
        //public _2558584 _2558584 { get; set; }
        //public _25939 _25939 { get; set; }
        //public _233219 _233219 { get; set; }
        //public _1234120 _1234120 { get; set; }
        //public _1763 _1763 { get; set; }
        //public _461226 _461226 { get; set; }
        //public _224794 _224794 { get; set; }
        //public _2558589 _2558589 { get; set; }
    }

    public class Feed
    {
        public int id { get; set; }
        public FeedProperties properties { get; set; }
    }

    public class FeedProperties
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public Nullable<bool> search_indexed { get; set; }
    }

    public class Social_Services
    {
        public Facebook facebook { get; set; }
        public Twitter twitter { get; set; }
        public Gravatar gravatar { get; set; }
        public Appdotnet appdotnet { get; set; }
        public Upload upload { get; set; }
    }

    public class Facebook
    {
        public bool syncing { get; set; }
        public string facebook_picture_url { get; set; }
        public string facebook_uid { get; set; }
    }

    public class Twitter
    {
        public string twitter_username { get; set; }
        public bool syncing { get; set; }
        public string twitter_picture_url { get; set; }
        public string twitter_uid { get; set; }
    }

    public class Gravatar
    {
        public string gravatar_picture_url { get; set; }
    }

    public class Appdotnet
    {
        public bool syncing { get; set; }
        public object appdotnet_uid { get; set; }
        public object appdotnet_picture_url { get; set; }
    }

    public class Upload
    {
        public object upload_picture_url { get; set; }
    }

    public class Starred_Counts
    {
        public int count { get; set; }
        public string feed_address { get; set; }
        public string tag { get; set; }
        public int? feed_id { get; set; }
    }

    public class Social_Feeds
    {
        public string username { get; set; }
        public int ps { get; set; }
        public int user_id { get; set; }
        public int subscription_user_id { get; set; }
        public string feed_link { get; set; }
        public string feed_address { get; set; }
        public int feed_opens { get; set; }
        public int num_subscribers { get; set; }
        public int shared_stories_count { get; set; }
        public bool? _private { get; set; }
        public string feed_title { get; set; }
        public bool? _protected { get; set; }
        public string location { get; set; }
        public string photo_url { get; set; }
        public string large_photo_url { get; set; }
        public bool is_trained { get; set; }
        public int ng { get; set; }
        public int nt { get; set; }
        public string id { get; set; }
        public string page_url { get; set; }
    }

}

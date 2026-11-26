namespace PullCloud;

public sealed class UsState
{
    public string Abbr { get; init; } = "";
    public string Name { get; init; } = "";
    public string DefaultTz { get; init; } = "";
    public string[] AltTz { get; init; } = Array.Empty<string>();
    public string? Note { get; init; }
    /// <summary>Normalizovan naziv grada -> IANA zona (lokalna baza, radi i bez interneta).</summary>
    public Dictionary<string, string> Cities { get; } = new();

    public bool IsSplit => AltTz.Length > 0;
    public IEnumerable<string> AllTz => new[] { DefaultTz }.Concat(AltTz);
    public override string ToString() => $"{Name} ({Abbr})" + (IsSplit ? "   ⚠ multiple time zones" : "");
}

public static class UsStates
{
    const string ET = "America/New_York", CT = "America/Chicago", MT = "America/Denver",
        PT = "America/Los_Angeles", AZ = "America/Phoenix", AK = "America/Anchorage",
        HI = "Pacific/Honolulu", ADAK = "America/Adak", BOISE = "America/Boise",
        IND = "America/Indiana/Indianapolis", DET = "America/Detroit", MENO = "America/Menominee",
        LOU = "America/Kentucky/Louisville";

    /// <summary>Cities listed under two different zones of the same state (should always be empty).</summary>
    public static readonly List<string> Conflicts = new();
    public static readonly List<UsState> All = Build();

    static UsState S(string a, string n, string tz) => new() { Abbr = a, Name = n, DefaultTz = tz };

    static UsState Split(string a, string n, string tz, string[] alt, string note, params (string tz, string cities)[] lists)
    {
        var s = new UsState { Abbr = a, Name = n, DefaultTz = tz, AltTz = alt, Note = note };
        foreach (var (z, cs) in lists)
            foreach (var c in cs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var k = Util.Norm(c);
                if (s.Cities.TryGetValue(k, out var prev) && prev != z) Conflicts.Add($"{a}: {c} ({prev} vs {z})");
                s.Cities[k] = z;
            }
        return s;
    }

    static List<UsState> Build() => new()
    {
        S("AL", "Alabama", CT),
        Split("AK", "Alaska", AK, new[] { ADAK }, "The Aleutian Islands (Adak, Atka) are on Hawaii-Aleutian time.",
            (AK, "Anchorage, Fairbanks, Juneau, Sitka, Ketchikan, Kodiak, Wasilla, Nome, Bethel, Unalaska, Palmer, Kenai, Homer, Valdez"),
            (ADAK, "Adak, Atka")),
        Split("AZ", "Arizona", AZ, new[] { MT }, "Arizona does NOT observe DST, except the Navajo Nation (Mountain time with DST).",
            (AZ, "Phoenix, Tucson, Mesa, Chandler, Scottsdale, Glendale, Gilbert, Tempe, Peoria, Surprise, Flagstaff, Yuma, Prescott, Sedona, Page, Lake Havasu City, Kingman, Goodyear, Avondale, Casa Grande, Sierra Vista, Bullhead City, Second Mesa, Kykotsmovi, Winslow, Holbrook, Show Low"),
            (MT, "Window Rock, Tuba City, Chinle, Kayenta, Ganado, Fort Defiance")),
        S("AR", "Arkansas", CT),
        S("CA", "California", PT),
        S("CO", "Colorado", MT),
        S("CT", "Connecticut", ET),
        S("DE", "Delaware", ET),
        S("DC", "District of Columbia", ET),
        Split("FL", "Florida", ET, new[] { CT }, "The Panhandle west of the Apalachicola River is on Central time.",
            (ET, "Miami, Orlando, Tampa, Jacksonville, Tallahassee, St Petersburg, Fort Lauderdale, West Palm Beach, Naples, Fort Myers, Cape Coral, Sarasota, Gainesville, Ocala, Daytona Beach, Key West, Port St Lucie, Lakeland, Hialeah, Hollywood, Pembroke Pines, Coral Springs, Miramar, Clearwater, Palm Bay, Melbourne, Boca Raton, Kissimmee, Sanford, Palm Coast, St Augustine, Apalachicola, Port St Joe, Perry, Live Oak, Lake City, Homestead, Deltona, Bradenton"),
            (CT, "Pensacola, Panama City, Panama City Beach, Fort Walton Beach, Destin, Crestview, Navarre, Milton, Niceville, DeFuniak Springs, Marianna, Chipley, Bonifay, Gulf Breeze, Lynn Haven, Callaway, Mexico Beach, Wewahitchka, Blountstown, Pace, Santa Rosa Beach, Freeport")),
        S("GA", "Georgia", ET),
        S("HI", "Hawaii", HI),
        Split("ID", "Idaho", BOISE, new[] { PT }, "The northern Idaho Panhandle is on Pacific time.",
            (BOISE, "Boise, Meridian, Nampa, Idaho Falls, Pocatello, Twin Falls, Caldwell, Rexburg, Eagle, Kuna, Mountain Home, Blackfoot, Burley, Jerome, Ketchum, Sun Valley, Salmon, McCall"),
            (PT, "Coeur d'Alene, Moscow, Lewiston, Sandpoint, Post Falls, Bonners Ferry, Kellogg, Grangeville, Hayden, Rathdrum, Orofino, Wallace, St Maries")),
        S("IL", "Illinois", CT),
        Split("IN", "Indiana", IND, new[] { CT }, "Most of Indiana is Eastern; the northwest (near Chicago) and southwest (near Evansville) are Central.",
            (IND, "Indianapolis, Fort Wayne, South Bend, Bloomington, Lafayette, West Lafayette, Muncie, Carmel, Fishers, Terre Haute, Kokomo, Anderson, Elkhart, Mishawaka, Richmond, Columbus, New Albany, Jeffersonville, Noblesville, Greenwood, Vincennes, Jasper, Marion, Logansport, Plymouth, Goshen, Warsaw, Clarksville, Westfield, Zionsville, Lawrence, Shelbyville, Seymour, Madison, Bedford"),
            (CT, "Gary, Hammond, East Chicago, Merrillville, Valparaiso, Michigan City, La Porte, LaPorte, Crown Point, Portage, Hobart, Schererville, Highland, Munster, Chesterton, Dyer, St John, Cedar Lake, Lowell, Griffith, Whiting, Evansville, Newburgh, Princeton, Mount Vernon, Boonville, Tell City, Rockport, Knox, Rensselaer, Kentland")),
        S("IA", "Iowa", CT),
        Split("KS", "Kansas", CT, new[] { MT }, "Four counties on the Colorado border are on Mountain time.",
            (CT, "Wichita, Topeka, Kansas City, Overland Park, Olathe, Lawrence, Manhattan, Salina, Hutchinson, Dodge City, Garden City, Liberal, Hays, Colby, Emporia, Leavenworth, Shawnee, Lenexa, Pittsburg"),
            (MT, "Goodland, Sharon Springs, Tribune, Syracuse, Kanorado")),
        Split("KY", "Kentucky", LOU, new[] { CT }, "Eastern Kentucky is Eastern time, western Kentucky is Central.",
            (LOU, "Louisville, Lexington, Frankfort, Covington, Florence, Richmond, Georgetown, Elizabethtown, Ashland, Nicholasville, Somerset, Pikeville, Hazard, London, Corbin, Danville, Winchester, Shelbyville, Radcliff, Fort Thomas, Newport, Erlanger, Independence, Bardstown, Campbellsville, Berea, Morehead, Maysville"),
            (CT, "Bowling Green, Owensboro, Paducah, Hopkinsville, Henderson, Madisonville, Murray, Glasgow, Mayfield, Russellville, Franklin, Columbia, Princeton, Benton, Leitchfield, Morgantown, Fort Campbell, Oak Grove, Central City, Greenville, Scottsville, Tompkinsville, Burkesville")),
        S("LA", "Louisiana", CT),
        S("ME", "Maine", ET),
        S("MD", "Maryland", ET),
        S("MA", "Massachusetts", ET),
        Split("MI", "Michigan", DET, new[] { MENO }, "Four Upper Peninsula counties bordering Wisconsin are on Central time.",
            (DET, "Detroit, Grand Rapids, Lansing, Ann Arbor, Flint, Kalamazoo, Marquette, Escanaba, Sault Ste Marie, Houghton, Traverse City, Saginaw, Warren, Sterling Heights, Dearborn, Livonia, Troy, Southfield, Novi, Pontiac, Battle Creek, Muskegon, Holland, Midland, Bay City, Jackson"),
            (MENO, "Menominee, Iron Mountain, Kingsford, Iron River, Ironwood, Norway, Crystal Falls, Bessemer, Wakefield")),
        S("MN", "Minnesota", CT),
        S("MS", "Mississippi", CT),
        S("MO", "Missouri", CT),
        S("MT", "Montana", MT),
        Split("NE", "Nebraska", CT, new[] { MT }, "Western Nebraska (the Panhandle) is on Mountain time.",
            (CT, "Omaha, Lincoln, Grand Island, Kearney, North Platte, Fremont, Hastings, Norfolk, Columbus, Bellevue, Papillion, McCook, Valentine, La Vista, South Sioux City, Beatrice, Lexington, York"),
            (MT, "Scottsbluff, Gering, Alliance, Sidney, Chadron, Ogallala, Kimball, Imperial, Grant, Bridgeport, Crawford, Mitchell, Rushville, Gordon, Oshkosh, Harrison")),
        Split("NV", "Nevada", PT, new[] { MT }, "West Wendover is on Mountain time.",
            (PT, "Las Vegas, Reno, Henderson, Carson City, Sparks, Elko, North Las Vegas, Mesquite, Boulder City, Fallon, Winnemucca, Pahrump, Ely"),
            (MT, "West Wendover")),
        S("NH", "New Hampshire", ET),
        S("NJ", "New Jersey", ET),
        S("NM", "New Mexico", MT),
        S("NY", "New York", ET),
        S("NC", "North Carolina", ET),
        Split("ND", "North Dakota", CT, new[] { MT }, "Southwestern North Dakota is on Mountain time.",
            (CT, "Fargo, Bismarck, Grand Forks, Minot, Williston, Mandan, Jamestown, West Fargo, Devils Lake, Valley City, Wahpeton"),
            (MT, "Dickinson, Bowman, Beach, Mott, Hettinger, Belfield, Medora, New England")),
        S("OH", "Ohio", ET),
        S("OK", "Oklahoma", CT),
        Split("OR", "Oregon", PT, new[] { BOISE }, "Most of Malheur County (east) is on Mountain time.",
            (PT, "Portland, Salem, Eugene, Bend, Medford, Gresham, Hillsboro, Beaverton, Corvallis, Klamath Falls, Pendleton, Baker City, Burns, Albany, Springfield, Grants Pass, Redmond, Roseburg, Astoria, Coos Bay, The Dalles, Hermiston, La Grande"),
            (BOISE, "Ontario, Nyssa, Vale, Jordan Valley")),
        S("PA", "Pennsylvania", ET),
        S("RI", "Rhode Island", ET),
        S("SC", "South Carolina", ET),
        Split("SD", "South Dakota", CT, new[] { MT }, "The western half of South Dakota is on Mountain time.",
            (CT, "Sioux Falls, Pierre, Aberdeen, Brookings, Watertown, Mitchell, Yankton, Vermillion, Huron, Mobridge, Madison, Chamberlain"),
            (MT, "Rapid City, Spearfish, Sturgis, Deadwood, Lead, Belle Fourche, Hot Springs, Custer, Lemmon, Box Elder, Wall, Hill City, Keystone, Philip, Martin")),
        Split("TN", "Tennessee", CT, new[] { ET }, "East Tennessee (Knoxville, Chattanooga...) is Eastern, the rest is Central.",
            (CT, "Nashville, Memphis, Clarksville, Murfreesboro, Franklin, Jackson, Cookeville, Columbia, Crossville, Tullahoma, Shelbyville, Brentwood, Hendersonville, Gallatin, Smyrna, Lebanon, Dickson, Bartlett, Collierville, Germantown, Spring Hill, La Vergne, Mount Juliet, Dyersburg, Union City, Paris, Manchester, McMinnville, Lawrenceburg, Pulaski, Winchester"),
            (ET, "Knoxville, Chattanooga, Johnson City, Kingsport, Bristol, Cleveland, Oak Ridge, Maryville, Morristown, Gatlinburg, Sevierville, Pigeon Forge, Athens, Greeneville, Elizabethton, Alcoa, Farragut, Lenoir City, Dayton, Collegedale, East Ridge, Red Bank, Signal Mountain, Harriman, Kingston, Jefferson City, Newport, Rogersville, LaFollette, Jellico, Oneida, Madisonville, Sweetwater")),
        Split("TX", "Texas", CT, new[] { MT }, "El Paso and far West Texas are on Mountain time.",
            (CT, "Houston, Dallas, San Antonio, Austin, Fort Worth, Arlington, Corpus Christi, Plano, Lubbock, Laredo, Amarillo, Midland, Odessa, Brownsville, McAllen, Waco, Abilene, San Angelo, Tyler, Beaumont, Killeen, Frisco, Irving, Garland, Pecos, Alpine, Marfa, Fort Stockton, Denton, McKinney, Round Rock, College Station, Galveston, Sugar Land, The Woodlands, Pasadena, Grand Prairie, Mesquite, Carrollton, Wichita Falls, Edinburg, Harlingen, Del Rio, Eagle Pass, Big Spring, Kerrville, New Braunfels, San Marcos, Temple, Longview, Texarkana, Conroe, Victoria"),
            (MT, "El Paso, Socorro, Horizon City, Anthony, Canutillo, Fabens, San Elizario, Clint, Sierra Blanca, Dell City, Vinton")),
        S("UT", "Utah", MT),
        S("VT", "Vermont", ET),
        S("VA", "Virginia", ET),
        S("WA", "Washington", PT),
        S("WV", "West Virginia", ET),
        S("WI", "Wisconsin", CT),
        S("WY", "Wyoming", MT),
        S("PR", "Puerto Rico", "America/Puerto_Rico"),
    };
}

public sealed record WorldPlace(string Name, string TzId)
{
    public override string ToString() => Name;
}

public static class WorldCities
{
    private static readonly (string name, string tz)[] Raw =
    {
        ("Sarajevo, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Banja Luka, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Bihac, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Mostar, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Tuzla, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Zenica, Bosnia and Herzegovina", "Europe/Sarajevo"),
        ("Belgrade, Serbia", "Europe/Belgrade"),
        ("Novi Sad, Serbia", "Europe/Belgrade"),
        ("Zagreb, Croatia", "Europe/Zagreb"),
        ("Split, Croatia", "Europe/Zagreb"),
        ("Podgorica, Montenegro", "Europe/Podgorica"),
        ("Skopje, North Macedonia", "Europe/Skopje"),
        ("Ljubljana, Slovenia", "Europe/Ljubljana"),
        ("Tirana, Albania", "Europe/Tirane"),
        ("Pristina, Kosovo", "Europe/Belgrade"),
        ("Vienna, Austria", "Europe/Vienna"),
        ("Berlin, Germany", "Europe/Berlin"),
        ("Frankfurt, Germany", "Europe/Berlin"),
        ("Munich, Germany", "Europe/Berlin"),
        ("Zurich, Switzerland", "Europe/Zurich"),
        ("London, United Kingdom", "Europe/London"),
        ("Dublin, Ireland", "Europe/Dublin"),
        ("Paris, France", "Europe/Paris"),
        ("Brussels, Belgium", "Europe/Brussels"),
        ("Amsterdam, Netherlands", "Europe/Amsterdam"),
        ("Rome, Italy", "Europe/Rome"),
        ("Milan, Italy", "Europe/Rome"),
        ("Madrid, Spain", "Europe/Madrid"),
        ("Barcelona, Spain", "Europe/Madrid"),
        ("Lisbon, Portugal", "Europe/Lisbon"),
        ("Copenhagen, Denmark", "Europe/Copenhagen"),
        ("Stockholm, Sweden", "Europe/Stockholm"),
        ("Oslo, Norway", "Europe/Oslo"),
        ("Helsinki, Finland", "Europe/Helsinki"),
        ("Warsaw, Poland", "Europe/Warsaw"),
        ("Prague, Czech Republic", "Europe/Prague"),
        ("Bratislava, Slovakia", "Europe/Bratislava"),
        ("Budapest, Hungary", "Europe/Budapest"),
        ("Sofia, Bulgaria", "Europe/Sofia"),
        ("Bucharest, Romania", "Europe/Bucharest"),
        ("Athens, Greece", "Europe/Athens"),
        ("Istanbul, Turkey", "Europe/Istanbul"),
        ("Kyiv, Ukraine", "Europe/Kyiv"),
        ("Moscow, Russia", "Europe/Moscow"),
        ("Dubai, United Arab Emirates", "Asia/Dubai"),
        ("Doha, Qatar", "Asia/Qatar"),
        ("Riyadh, Saudi Arabia", "Asia/Riyadh"),
        ("Tel Aviv, Israel", "Asia/Jerusalem"),
        ("Cairo, Egypt", "Africa/Cairo"),
        ("Johannesburg, South Africa", "Africa/Johannesburg"),
        ("Mumbai, India", "Asia/Kolkata"),
        ("New Delhi, India", "Asia/Kolkata"),
        ("Bangkok, Thailand", "Asia/Bangkok"),
        ("Singapore", "Asia/Singapore"),
        ("Manila, Philippines", "Asia/Manila"),
        ("Hong Kong", "Asia/Hong_Kong"),
        ("Shanghai, China", "Asia/Shanghai"),
        ("Tokyo, Japan", "Asia/Tokyo"),
        ("Seoul, South Korea", "Asia/Seoul"),
        ("Sydney, Australia", "Australia/Sydney"),
        ("Melbourne, Australia", "Australia/Melbourne"),
        ("Perth, Australia", "Australia/Perth"),
        ("Auckland, New Zealand", "Pacific/Auckland"),
        ("New York, USA (Eastern)", "America/New_York"),
        ("Chicago, USA (Central)", "America/Chicago"),
        ("Denver, USA (Mountain)", "America/Denver"),
        ("Phoenix, USA (Arizona)", "America/Phoenix"),
        ("Los Angeles, USA (Pacific)", "America/Los_Angeles"),
        ("Anchorage, USA (Alaska)", "America/Anchorage"),
        ("Honolulu, USA (Hawaii)", "Pacific/Honolulu"),
        ("Toronto, Canada", "America/Toronto"),
        ("Vancouver, Canada", "America/Vancouver"),
        ("Mexico City, Mexico", "America/Mexico_City"),
        ("Bogota, Colombia", "America/Bogota"),
        ("Sao Paulo, Brazil", "America/Sao_Paulo"),
        ("Buenos Aires, Argentina", "America/Argentina/Buenos_Aires"),
    };

    /// <summary>Alphabetical list of places whose time zone exists on this PC.</summary>
    public static readonly List<WorldPlace> All = Raw
        .Where(r => Tz.TryGet(r.tz, out _))
        .Select(r => new WorldPlace(r.name, r.tz))
        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

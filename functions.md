# Functions

Following functions are available to be used in the yaml file:

- `<prefab>`: Original prefab id.
- `<safeprefab>`: Original prefab id with underscores replaced by dashes.
  - This can be used as workaround because underscores split the prefab id as separate parameters.
- `<zdo>`: Object id.
- `<biome>`: Biome where the object is located.
- `<x>`, `<y>` and `<z>`: Object center point.
- `<pos>`: Object center point as x,z,y.
- `<i>` and `<j>`: Object zone indices.
- `<a>`: Object rotation.
- `<rot>`: Object rotation as y,x,z.
- `<key_*>`: Global key value.
- `<int_*>`: Integer value from the object data.
- `<float_*>`: Decimal value from the object data.
- `<long_*>`: Big integer value from the object data.
- `<string_*>`: Text value from the object data.
- `<bool_*>`: Integer value from the object converted to true or false.
- `<hash_*>`: Integer value from the object converted to prefab name.
- `<vec_*>`: Vector3 value from the object converted to x,z,y.
- `<quat_*>`: Quaternion value from the object converted to y,x,z.
- `<byte_*>`: Byte value from the object converted to base64 text.
- `<zdo_*>`: Object id value from the object.
- `<amount_X_Y>`: Amount of item at slot X,Y.
- `<durability_X_Y>`: Durability of item at slot X,Y.
- `<quality_X_Y>`: Quality of item at slot X,Y.
- `<item_*>`: Amount of specific item in the container.
  - Wildcard `*` can be used for partial matches. For example `Trophy*` to match all trophies or `*` to count everything.
- `<item_X_Y>`: Item name at slot X,Y.
- `<pdata_*>`: Player data.
  - `<pdata_baseValue>`: Amount of nearby player base structures.
  - `<pdata_possibleEvents>`: List of possible events.
- `<pid>`: Steam/Playfab id of the client that controls the object.
  - The client always controls its player object.
- `<platform>`: Platform of the client that controls the object.
  - Can be combined with `<pid>` to get full id.
- `<pname>`: Player name of the client that controls the object.
- `<cid>`: Character id of the client that controls the object.
- `<pvisible>`: Whether the player has public visibility enabled (true or false).
- `<owner>`: Id of the owner client (long number).
- `<connected>`: Id of the connected object (object id).
- `<none>` Empty or lack of value when using filters.
- `<joints>`: List of all transformation names in the object.
  - This info can be used when attaching objects.

Object attributes can be queried with the field system. For example `<float_WearNTear.m_health>` to get piece maximum health or `<float_ItemDrop.m_itemData.m_shared.m_maxDurability>` to get item maximum durability.

For missing object data, the default value can be set by adding `=value`. For example `<int_level=1>`.

## Text

- `<findlower_X>`: Returns the lowercase letters in text X.
- `<findupper_X>`: Returns the uppercase letters in text X.
- `<hashof_X>`: Returns hash of the text X.
- `<left_X_Y>`: Returns the leftmost Y characters of text X.
- `<len_X>`: Returns length of the text X.
- `<lower_X>`: Returns lower case of the text X.
- `<mid_X_Y_Z>`: Returns Z characters from text X starting at position Y.
- `<par>`: Returns the whole parameter.
- `<par_X`>: Returns parameter X.
- `<proper_X>`: Returns text X with proper case (first letter of each word capitalized).
- `<rest_X>`: Returns the rest of the text starting from par X.
- `<right_X_Y>`: Returns the rightmost Y characters of text X.
- `<search_X_Y_Z>`: Searches for text X in text Y starting at position Z, returns position or default.
- `<textof_X>`: Returns text of the hash X.
- `<trim_X>`: Returns text X without leading and trailing spaces.
- `<upper_X>`: Returns upper case of the text X.

## Numeric

- `<abs_X>`: Returns absolute value of the number X.
- `<add_X_Y>`: Returns sum of X and Y.
  - Supports any number of parameters.
- `<asin_X>`: Returns arcsine of X.
- `<acos_X>`: Returns arccosine of X.
- `<atan_X>`: Returns arctangent of X.
- `<atan_X_Y>`: Returns arctangent of X/Y.
- `<calcfloat_X>`: Evaluates the math expression X and returns a decimal number.
- `<calcint_X>`: Evaluates the math expression X and returns an integer number.
- `<ceil_X>`: Returns smallest integer greater than or equal to X.
- `<cos_X>`: Returns cosine of X.
- `<div_X_Y>`: Returns quotient of X and Y.
  - Supports any number of parameters.
- `<eq_X_Y>`: Returns "true" if X equals Y, "false" otherwise.
- `<even_X>`: Returns "true" if X is even, "false" if odd.
- `<exp_X>`: Returns e raised to the power of X.
- `<floor_X>`: Returns largest integer less than or equal to X.
- `<ge_X_Y>`: Returns "true" if X is greater than or equal to Y, "false" otherwise.
- `<gt_X_Y>`: Returns "true" if X is greater than Y, "false" otherwise.
- `<iter_OP_MINI_MAXI_TEMPLATE=default>`: Expands TEMPLATE over index `i` from MINI to MAXI and reduces results with OP.
  - Example: `<iter_add_0_10_i>` returns 55.
  - Example: `<iter_add_0_7_i=-999>`.
  - `i` is replaced only when used as a standalone token (for example `amount_i`, but not `limit`).
- `<large_X_Y...>`: Returns the Xth largest value from the list of numbers Y....
  - If X is less than 1, it returns the largest value.
  - If X is greater than the amount of numbers, it returns the smallest value.
- `<le_X_Y>`: Returns "true" if X is less than or equal to Y, "false" otherwise.
- `<log_X>`: Returns natural logarithm of X.
- `<log_X_Y>`: Returns logarithm of X with base Y.
- `<lt_X_Y>`: Returns "true" if X is less than Y, "false" otherwise.
- `<max_X_Y>`: Returns maximum of X and Y.
- `<min_X_Y>`: Returns minimum of X and Y.
- `<mod_X_Y>`: Returns remainder of X divided by Y.
- `<mul_X_Y>`: Returns product of X and Y.
  - Supports any number of parameters.
- `<ne_X_Y>`: Returns "true" if X is not equal to Y, "false" otherwise.
- `<odd_X>`: Returns "true" if X is odd, "false" if even.
- `<iter_OP_MINI_MAXI_TEMPLATE=default>`: Expands TEMPLATE over index `i` from MINI to MAXI and reduces results with OP.
  - Example: `<iter_add_0_10_i>` returns 55.
  - Default value applies to the TEMPLATE.
- `<iter2_OP_MINI_MAXI_MINJ_MAXJ_TEMPLATE=default>`: Expands TEMPLATE over indexes `i` and `j`, then reduces results with OP.
  - `i` loops from MINI to MAXI and `j` loops from MINJ to MAXJ.
- `<pow_X_Y>`: Returns X raised to the power of Y.
- `<randf_X_Y>`: Returns random decimal number between X and Y.
- `<randi_X_Y>`: Returns random integer number between X and Y.
- `<randomfloat_X_Y>`: Returns random decimal number between X and Y.
- `<randomint_X_Y>`: Returns random integer number between X and Y.
- `<rank_X_Y...>`: Returns how many numbers in the list Y... are greater than X.
- `<round_X>`: Returns nearest integer of X.
- `<sin_X>`: Returns sine of X.
- `<small_X_Y...>`: Returns the Xth smallest value from the list of numbers Y....
  - If X is less than 1, it returns the smallest value.
  - If X is greater than the amount of numbers, it returns the largest value.
- `<sqrt_X>`: Returns square root of X.
- `<sub_X_Y>`: Returns difference of X and Y.
  - Supports any number of parameters.
- `<tan_X>`: Returns tangent of X.
- `<rad2deg_X>`: Converts radians X to degrees.
- `<deg2rad_X>`: Converts degrees X to radians.
- `<vec2deg_X_Z>`: Returns `atan2(Z, X)` from a 2D vector.
- `<vec2rad_X_Z>`: Returns `atan2(Z, X)` with an additional degrees-to-radians factor.

## Vector

Vector related functions (vectors use x,z,y order):

- `<add_A_B>`: Returns sum of vectors A and B.
  - Scalars add to each component.
  - Supports any number of parameters.
- `<angle_A_B>`: Returns angle in degrees between vectors A and B.
- `<distance_A_B>`: Returns distance between vectors A and B.
- `<div_A_B>`: Returns quotient of vectors A and B.
  - Scalars divide each component.
  - Supports any number of parameters.
- `<dot_A_B>`: Returns dot product of vectors A and B.
- `<cross_A_B>`: Returns cross product vector of A and B.
- `<normalize_V>`: Returns normalized vector V.
- `<magnitude_V>`: Returns magnitude of vector V.
- `<mul_A_B>`: Returns product of vectors A and B.
  - Scalars multiply each component.
  - Supports any number of parameters.
- `<sqrmagnitude_V>`: Returns squared magnitude of vector V.
- `<sub_A_B>`: Returns difference of vectors A and B.
  - Scalars subtract from each component.
  - Supports any number of parameters.
- `<project_V_N>`: Returns projection of vector V onto vector N.
- `<reflect_D_N>`: Returns reflection vector of direction D and normal N.
- `<lerp_A_B_T>`: Returns interpolation between vectors A and B with factor T.
- `<rad2vec_X>`: Converts radians X to a direction vector.
- `<deg2vec_X>`: Converts degrees X to a direction vector.
- `<vecx_V>`: Returns X axis component of vector V.
- `<vecy_V>`: Returns Y axis component of vector V.
- `<vecz_V>`: Returns Z axis component of vector V.

## Long numbers

Long number related functions (usually only needed when calculating with game ticks):

- `<addlong_X_Y>`: Returns sum of X and Y as long number.
- `<calclong_X>`: Evaluates the math expression X and returns a long number.
- `<divlong_X_Y>`: Returns quotient of X and Y as long number.
- `<modlong_X_Y>`: Returns remainder of X divided by Y as long number.
- `<mullong_X_Y>`: Returns product of X and Y as long number.
- `<sublong_X_Y>`: Returns difference of X and Y as long number.

## Custom data

Custom data related functions:

- `<clear_X>`: Removes custom data with key X.
  - Wildcard * in the key name can be used to remove multiple keys at once.
- `<load_X=default>`: Gets custom data with key X. If not found, returns the given default value.
- `<save_X_Y>`: Saves custom data with key X and value Y.
  - Wildcard * in the key name can be used to set multiple keys at once (these keys must already exist).
- `<save++_X>`: Shorthand for increasing the value of key X by 1. Missing keys are created with value 1.
- `<save--_X>`: Shorthand for decreasing the value of key X by 1. Missing keys are created with value -1.

Custom data can be used to replace global keys. The biggest benefit is that custom data is not sent to clients, which reduces network traffic and keeps them hidden from players.

## Time

Time related functions:

- `<day>`: Days since the world start (int type).
- `<time>`: Seconds since the world start (float type).
  - Each day is 1800 seconds.
- `<ticks>`: Ticks since the world start (long type).
  - Each second is 10000000 ticks.
- `<time_X>`: Formats game time using format string X.
  - Game time is converted from seconds to a date starting from January 1, 2000.
  - Uses the game's day length system (default 1800 seconds per day).
  - Supports .NET DateTime format strings (e.g., `<time_yyyy-MM-dd HH:mm:ss>`).
- `<realtime>`: Seonds since Unix epoch (January 1, 1970) in UTC (long type).
- `<realtime_X>`: Formatted real-world time.
  - Supports .NET DateTime format strings (e.g., `<realtime_yyyy-MM-dd HH:mm:ss>`).
  - Uses the server timezone.
- `<realtime_X_Y>`: Formatted real-world time with custom timezone.
  - This can be used if the server timezone is different from desired timezone.
  - Example: `<realtime_HH:mm_-5>` for Eastern Standard Time.

## Functions

Custom functions: See [Expand World Code](https://github.com/JereKuusela/valheim-expand_world_code).

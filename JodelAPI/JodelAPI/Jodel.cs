﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using JodelAPI.Internal;
using JodelAPI.Json;
using JodelAPI.Json.Request;
using JodelAPI.Json.Response;
using JodelAPI.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JodelAPI
{
    public class Jodel
    {
        #region Fields and Properties

        public User Account { get; private set; }

        #endregion

        #region Constructor

        public Jodel(User user)
        {
            Account = user;
        }

        public Jodel(string place, string countryCode, string cityName, bool createToken = true)
            : this(new User
            {
                CountryCode = countryCode,
                CityName = cityName,
                Place = new Location(place),
            })
        {
            if (createToken)
                GenerateAccessToken();
        }

        #endregion

        #region Methods

        #region Account

        public bool GenerateAccessToken(WebProxy proxy = null)
        {
            return Account.Token.GenerateNewAccessToken(proxy);
        }

        public bool RefreshAccessToken()
        {
            return Account.Token.RefreshAccessToken();
        }

        public void GetUserConfig()
        {
            string jsonString = Links.GetUserConfig.ExecuteRequest(Account);

            JsonConfig.RootObject config = JsonConvert.DeserializeObject<JsonConfig.RootObject>(jsonString);

            List<User.Experiment> experiments = new List<User.Experiment>(config.experiments.Count);
            experiments.AddRange(config.experiments.Select(experiment => new User.Experiment(experiment.name, experiment.@group, experiment.features)));

            List<Channel> channels = new List<Channel>(config.followed_hashtags.Count);
            channels.AddRange(config.followed_channels.Select(channelname => new Channel(channelname, true)));

            Account.ChannelsFollowLimit = config.channels_follow_limit;
            Account.Experiments = experiments;
            Account.HomeName = config.home_name;
            Account.HomeSet = config.home_set;
            Account.FollowedHashtags = config.followed_hashtags;
            Account.Location = config.location;
            Account.Moderator = config.moderator;
            Account.TripleFeedEnabled = config.triple_feed_enabled;
            Account.UserType = config.user_type;
            Account.Verified = config.verified;
            Account.FollowedChannels = channels;
        }

        public int GetKarma()
        {
            string jsonString = Links.GetKarma.ExecuteRequest(Account);

            JsonKarma.RootObject karma = JsonConvert.DeserializeObject<JsonKarma.RootObject>(jsonString);
            return karma.karma;
        }

        public void SetLocation()
        {
            JsonRequestSetLocation payload = new JsonRequestSetLocation
            {
                location =
                {
                    city = Account.CityName,
                    country = Account.CountryCode,
                    loc_accuracy = 0.0,
                    name = Account.CityName,
                    loc_coordinates =
                    {
                        lat = Account.Place.Latitude,
                        lng = Account.Place.Longitude
                    }
                }
            };

            Links.SendUserLocation.ExecuteRequest(Account, payload: payload);
        }

        public Captcha GetCaptcha(bool advanced = false)
        {
            var captchaRoot = Links.GetCaptcha.GetCaptcha(Account);
            return advanced ? new Captcha(new JodelWebClient().DownloadData(captchaRoot.image_url), captchaRoot.key, captchaRoot.image_url, captchaRoot.image_size) : new Captcha(new JodelWebClient().DownloadData(captchaRoot.image_url), captchaRoot.key, captchaRoot.image_url);
        }

        public bool SolveCaptcha(Captcha captcha, int[] answer)
        {
            return Links.VerifyCaptcha.PostCaptcha(Account, captcha, answer);
        }

        public bool VerifyAutomatically()
        {
            var captcha = GetCaptcha();
            MD5 md5 = MD5.Create();
            byte[] bhash = md5.ComputeHash(captcha.Image);
            File.WriteAllBytes("test.png", captcha.Image);
            StringBuilder sb = new StringBuilder();
            foreach (byte t in bhash)
            {
                sb.Append(t.ToString("x2"));
            }
            string hash = sb.ToString();
            int[] answer = Captcha.Solutions.FirstOrDefault(t => t.Key == hash).Value;
            return SolveCaptcha(captcha, answer);
        }

        #endregion

        #region Channels

        public IEnumerable<Channel> GetRecommendedChannels()
        {
            string jsonString = Links.GetRecommendedChannels.ExecuteRequest(Account, new Dictionary<string, string> { { "home", "false" } }, payload: new JsonRequestRecommendedChannels());

            JsonRecommendedChannels.RootObject channels = JsonConvert.DeserializeObject<JsonRecommendedChannels.RootObject>(jsonString);

            List<Channel> recommendedChannels = new List<Channel>();
            foreach (JsonRecommendedChannels.Recommended recommended in channels.recommended)
            {
                if (Account.FollowedChannels.Any(x => x.ChannelName == recommended.channel))
                {
                    Channel ch = Account.FollowedChannels.First(x => x.ChannelName == recommended.channel).UpdateProperties(recommended.image_url, recommended.followers);
                    recommendedChannels.Add(ch);
                }
                else
                {
                    recommendedChannels.Add(new Channel(recommended.channel) { ImageUrl = recommended.image_url, Followers = recommended.followers });
                }
            }

            return recommendedChannels;
        }

        public IEnumerable<Channel> GetFollowedChannelsMeta(bool home = false)
        {
            JsonRequestFollowedChannelMeta payload = new JsonRequestFollowedChannelMeta();
            foreach (Channel channel in Account.FollowedChannels.Where(x => x.Following))
            {
                payload.Values.Add(channel.ChannelName, -1);
            }
            string jsonString = Links.GetFollowedChannelsMeta.ExecuteRequest(Account, new Dictionary<string, string> { { "home", home.ToString().ToLower() } }, payload);

            JsonFollowedChannelsMeta.RootObject data = JsonConvert.DeserializeObject<JsonFollowedChannelsMeta.RootObject>(jsonString);
            
            return data.channels.Select(channel => Account.FollowedChannels
                .FirstOrDefault(x => x.ChannelName == channel.channel)?
                .UpdateProperties(channel.followers, channel.sponsored, channel.unread))
                .Where(c => c != null);
        }

        #endregion

        #region Jodels

        public JodelMainData GetPostLocationCombo(bool stickies = false, bool home = false)
        {
            string jsonString = Links.GetPostsCombo.ExecuteRequest(Account, new Dictionary<string, string>
            {
                { "lat", Account.Place.Latitude.ToString("F",CultureInfo.InvariantCulture) },
                { "lng", Account.Place.Longitude.ToString("F",CultureInfo.InvariantCulture) },
                { "stickies", stickies.ToString().ToLower() },
                { "home", home.ToString().ToLower() }
            });

            JsonJodelsFirstRound.RootObject jodels = JsonConvert.DeserializeObject<JsonJodelsFirstRound.RootObject>(jsonString);
            JodelMainData data = new JodelMainData { Max = jodels.max };
            data.RecentJodels.AddRange(jodels.recent.Select(r => new JodelPost(r)));
            data.RepliedJodels.AddRange(jodels.replied.Select(r => new JodelPost(r)));
            data.VotedJodels.AddRange(jodels.voted.Select(v => new JodelPost(v)));
            return data;
        }

        public JodelMainData GetPostHashtagCombo(string hashtag, bool home = false)
        {
            string jsonString = Links.GetHashtagCombo.ExecuteRequest(Account, new Dictionary<string, string>
            {
                { "hashtag", hashtag},
                { "home", home.ToString().ToLower() }
            });

            JsonJodelsFirstRound.RootObject jodels = JsonConvert.DeserializeObject<JsonJodelsFirstRound.RootObject>(jsonString);
            JodelMainData data = new JodelMainData { Max = jodels.max };
            data.RecentJodels.AddRange(jodels.recent.Select(r => new JodelPost(r)));
            data.RepliedJodels.AddRange(jodels.replied.Select(r => new JodelPost(r)));
            data.VotedJodels.AddRange(jodels.voted.Select(v => new JodelPost(v)));
            return data;
        }

        public JodelMainData GetPostChannelCombo(string channel, bool home = false)
        {
            string jsonString = Links.GetChannelCombo.ExecuteRequest(Account, new Dictionary<string, string>
            {
                { "channel", channel},
                { "home", home.ToString().ToLower() }
            });

            JsonJodelsFirstRound.RootObject jodels = JsonConvert.DeserializeObject<JsonJodelsFirstRound.RootObject>(jsonString);
            JodelMainData data = new JodelMainData { Max = jodels.max };
            data.RecentJodels.AddRange(jodels.recent.Select(r => new JodelPost(r)));
            data.RepliedJodels.AddRange(jodels.replied.Select(r => new JodelPost(r)));
            data.VotedJodels.AddRange(jodels.voted.Select(v => new JodelPost(v)));
            return data;
        }

        public IEnumerable<JodelPost> GetRecentPostsAfter(string afterPostId, bool home = false)
        {
            string jsonString = Links.GetMostRecentPosts.ExecuteRequest(Account, new Dictionary<string, string>
            {
                { "after", afterPostId },
                { "lat", Account.Place.Latitude.ToString(CultureInfo.InvariantCulture) },
                { "lng", Account.Place.Longitude.ToString(CultureInfo.InvariantCulture) },
                { "home", home.ToString().ToLower() }
            });

            return JsonConvert.DeserializeObject<JsonPostJodels.RootObject>(jsonString).posts.Select(p => new JodelPost(p));
        }

        public void Upvote(string postId, JodelPost.UpvoteReason reason = JodelPost.UpvoteReason.Stub, WebProxy proxy = null)
        {
            Links.UpvotePost.ExecuteRequest(Account, payload: new JsonRequestUpDownVote { reason_code = (int)reason }, postId: postId, proxy: proxy);
        }

        public void Downvote(string postId, JodelPost.DownvoteReason reason = JodelPost.DownvoteReason.Stub, WebProxy proxy = null)
        {
            Links.DownvotePost.ExecuteRequest(Account, payload: new JsonRequestUpDownVote { reason_code = (int)reason }, postId: postId, proxy: proxy);
        }

        /// <summary>
        /// Posts a Jodel and returns the PostId
        /// </summary>
        /// <param name="message">Text to post to Jodel</param>
        /// <param name="parentPostId">Comment to this post</param>
        /// <param name="color">Color of Jodel</param>
        /// <param name="image">Image to be sent</param>
        /// <param name="home">Post at home</param>
        /// <param name="proxy">The proxy to use</param>
        /// <returns>PostId</returns>
        public string Post(string message, string parentPostId = null, JodelPost.PostColor color = JodelPost.PostColor.Random, byte[] image = null, bool home = false, WebProxy proxy = null)
        {
            JsonRequestPostJodel payload = new JsonRequestPostJodel
            {
                location =
                {
                    city = Account.CityName,
                    name = Account.CityName,
                    country = Account.CountryCode,
                    loc_coordinates =
                    {
                        lat = Account.Place.Latitude,
                        lng = Account.Place.Longitude
                    }
                },
                color = color.ToString(),
                message = message,
                ancestor = parentPostId,
                to_home = home,
                image = image == null ? null : Convert.ToBase64String(image)
            };
            string jsonString = Links.SendPost.ExecuteRequest(Account, payload: payload, proxy: proxy);
            JsonPostJodel.RootObject data = JsonConvert.DeserializeObject<JsonPostJodel.RootObject>(jsonString);
            return data.post_id;
        }

        public JodelPost GetPost(string postId)
        {
            string jsonString = Links.GetPost.ExecuteRequest(Account, postId: postId);
            return new JodelPost(JsonConvert.DeserializeObject<JsonPostJodels.Post>(jsonString));
        }

        public JodelPost GetPostDetails(string postId, bool details = true, bool reversed = false, int next = 0)
        {
            Dictionary<string, string> parameters =
                new Dictionary<string, string> {{"details", details.ToString().ToLower()}};
            if (next > 0) parameters.Add("reply", next.ToString());
            parameters.Add("reversed", reversed.ToString().ToLower());

            string jsonString = Links.GetPostDetails.ExecuteRequest(Account, postId: postId, parameters: parameters);
            return new JodelPost(JsonConvert.DeserializeObject<JsonPostDetail.RootObject>(jsonString));
        }

        public string SharePost(string postId)
        {
            string jsonString = Links.GetShareUrl.ExecuteRequest(Account, postId: postId);
            return JObject.Parse(jsonString).GetValue("url").ToString();
        }

        #endregion

        #endregion
    }
}

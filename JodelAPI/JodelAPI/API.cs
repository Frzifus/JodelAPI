﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Web;
using System.IO;

namespace JodelAPI
{
    public static class API
    {
        public static string accessToken = "";
        public static string latitude = "";
        public static string longitude = "";

        private static List<Tuple<string, string, string, bool>> jodelCache = new List<Tuple<string, string, string, bool>>(); // postid, message, hexcolor, isImage
        private static string lastPostID = "";


        public static List<Tuple<string, string, string, bool>> GetFirstJodels()
        {
            string plainJson = GetPageContent("https://api.go-tellm.com/api/v2/posts/location/combo?lat=" + latitude + "&lng=" + longitude + "&access_token=" + accessToken);
            JodelsFirstRound.RootObject jfr = JsonConvert.DeserializeObject<JodelsFirstRound.RootObject>(plainJson);
            List<Tuple<string, string, string, bool>> temp = new List<Tuple<string, string, string, bool>>(); // List<post_id,message>
            int i = 0;
            foreach (var item in jfr.recent)
            {
                string msg = item.message;
                bool isURL = false;
                if (msg == "Jodel")
                {
                    msg = "http:"+item.image_url;
                    isURL = true;
                }

                temp.Add(new Tuple<string, string, string, bool>(item.post_id, msg, item.color, isURL));

                i++;
            }

            lastPostID = FilterItem(temp, temp.IndexOf(temp.Last()), false); // Set the last post_id for next jodels

            return temp;
        }

        public static List<Tuple<string, string, string, bool>> GetNextJodels()
        {
            List<Tuple<string, string, string, bool>> temp = new List<Tuple<string, string, string, bool>>(); // List<counter,post_id,message>
            for (int e = 0; e < 3; e++)
            {
                string plainJson = GetPageContent("https://api.go-tellm.com/api/v2/posts/location?lng=" + longitude + "&lat=" + latitude + "&after=" + lastPostID + "&access_token=" + accessToken + "&limit=1000000");
                JodelsLastRound.RootObject jlr = JsonConvert.DeserializeObject<JodelsLastRound.RootObject>(plainJson);
                int i = 0;
                foreach (var item in jlr.posts)
                {
                    string msg = item.message;
                    bool isURL = false;
                    if (msg == "Jodel")
                    {
                        msg = "http:" + item.image_url; // WELL THERE IS NO IMAGE_URL!!!!???
                        isURL = true;
                    }

                    temp.Add(new Tuple<string, string, string, bool>(item.post_id, msg, item.color, isURL));
                    i++;
                }

                lastPostID = FilterItem(temp, temp.IndexOf(temp.Last()), false); // Set the last post_id for next jodels
            }
            return temp;
        }

        public static List<Tuple<string, string, string, bool>> GetAllJodels()
        {
            List<Tuple<string, string, string, bool>> allJodels = new List<Tuple<string, string, string, bool>>();
            allJodels = GetFirstJodels();
            allJodels.AddRange(GetNextJodels());
            jodelCache = allJodels;
            return allJodels;
        }

        public static string FilterItem(List<Tuple<string, string, string, bool>> unfiltered, int index, bool filterMessage)
        {
            if (!filterMessage)
            {
                return unfiltered[index].Item1;
            }
            else
            {
                return unfiltered[index].Item2;
            }
        }

        public static void Upvote(string postID)
        {
            using (var client = new WebClient())
            {
                client.UploadData("https://api.go-tellm.com/api/v2/posts/" + postID + "/upvote?access_token=" + accessToken, "PUT", new byte[] { });
            }
        }

        public static void Upvote(int indexOfItem)
        {
            string postID = FilterItem(jodelCache, indexOfItem, false);

            using (var client = new WebClient())
            {
                client.UploadData("https://api.go-tellm.com/api/v2/posts/" + postID + "/upvote?access_token=" + accessToken, "PUT", new byte[] { });
            }
        } // cached List<> only

        public static void Downvote(string postID)
        {
            using (var client = new WebClient())
            {
                client.UploadData("https://api.go-tellm.com/api/v2/posts/" + postID + "/downvote?access_token=" + accessToken, "PUT", new byte[] { });
            }
        }

        public static void Downvote(int indexOfItem)
        {
            string postID = FilterItem(jodelCache, indexOfItem, false);

            using (var client = new WebClient())
            {
                client.UploadData("https://api.go-tellm.com/api/v2/posts/" + postID + "/downvote?access_token=" + accessToken, "PUT", new byte[] { });
            }
        } // cached List<> only

        public static int GetKarma()
        {
            return Convert.ToInt32(GetPageContent("https://api.go-tellm.com/api/v2/users/karma?access_token=" + accessToken));
        }

        public static void PostJodel()
        {

        }

        //public static List<Tuple<string, string>> SortMostCommented()
        //{

        //    return mostCommented;
        //}


        private static string GetPageContent(string link)
        {
            string html = string.Empty;
            WebRequest request = WebRequest.Create(link);
            WebResponse response = request.GetResponse();
            Stream data = response.GetResponseStream();
            using (StreamReader sr = new StreamReader(data))
            {
                html = sr.ReadToEnd();
            }
            return html;
        }
    }
}
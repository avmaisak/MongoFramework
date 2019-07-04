﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoFramework.Attributes;
using MongoFramework.Infrastructure;
using MongoFramework.Infrastructure.Linq;
using MongoFramework.Infrastructure.Mapping;
using MongoFramework.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MongoFramework.Tests.Linq
{
	[TestClass]
	public class LinqExtensionsTests : TestBase
	{
		public class LinqExtensionsModel
		{
			public string Id { get; set; }
		}
		public class WhereIdMatchesGuidModel
		{
			public Guid Id { get; set; }
			public string Description { get; set; }
		}
		public class WhereIdMatchesObjectIdModel
		{
			public ObjectId Id { get; set; }
			public string Description { get; set; }
		}
		public class WhereIdMatchesStringModel
		{
			public string Id { get; set; }
			public string Description { get; set; }
		}

		public class SearchTextModel
		{
			public string Id { get; set; }
			[Index(IndexType.Text)]
			public string Text { get; set; }
			public int MiscField { get; set; }
		}

		public class SearchGeoModel
		{
			public string Id { get; set; }
			public string Description { get; set; }
			[Index(IndexType.Geo2dSphere)]
			public GeoJsonPoint<GeoJson2DGeographicCoordinates> PrimaryCoordinates { get; set; }
			[Index(IndexType.Geo2dSphere)]
			public GeoJsonPoint<GeoJson2DGeographicCoordinates> SecondaryCoordinates { get; set; }

			[ExtraElements]
			public IDictionary<string, object> ExtraElements { get; set; }
			public double CustomDistanceField { get; set; }
		}

		[TestMethod]
		public void ValidToQuery()
		{
			EntityMapping.RegisterType(typeof(LinqExtensionsModel));

			var connection = TestConfiguration.GetConnection();
			var provider = new MongoFrameworkQueryProvider<LinqExtensionsModel>(connection);
			var queryable = new MongoFrameworkQueryable<LinqExtensionsModel>(provider);
			var result = LinqExtensions.ToQuery(queryable);

			Assert.AreEqual("db.LinqExtensionsModel.aggregate([])", result);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException), "ArgumentException")]
		public void InvalidToQuery()
		{
			LinqExtensions.ToQuery(null);
		}

		[TestMethod]
		public void WhereIdMatchesGuids()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesGuidModel>(TestConfiguration.GetConnection());
			var entityCollection = new EntityCollection<WhereIdMatchesGuidModel>()
			{
				new WhereIdMatchesGuidModel { Description = "1" },
				new WhereIdMatchesGuidModel { Description = "2" },
				new WhereIdMatchesGuidModel { Description = "3" },
				new WhereIdMatchesGuidModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var provider = new MongoFrameworkQueryProvider<WhereIdMatchesGuidModel>(connection);
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesGuidModel>(provider);
			
			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void WhereIdMatchesObjectIds()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesObjectIdModel>(connection);
			var entityCollection = new EntityCollection<WhereIdMatchesObjectIdModel>()
			{
				new WhereIdMatchesObjectIdModel { Description = "1" },
				new WhereIdMatchesObjectIdModel { Description = "2" },
				new WhereIdMatchesObjectIdModel { Description = "3" },
				new WhereIdMatchesObjectIdModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var provider = new MongoFrameworkQueryProvider<WhereIdMatchesObjectIdModel>(connection);
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesObjectIdModel>(provider);
			
			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void WhereIdMatchesStringIds()
		{
			var connection = TestConfiguration.GetConnection();
			var writerPipeline = new EntityWriterPipeline<WhereIdMatchesStringModel>(connection);
			var entityCollection = new EntityCollection<WhereIdMatchesStringModel>()
			{
				new WhereIdMatchesStringModel { Description = "1" },
				new WhereIdMatchesStringModel { Description = "2" },
				new WhereIdMatchesStringModel { Description = "3" },
				new WhereIdMatchesStringModel { Description = "4" }
			};
			writerPipeline.AddCollection(entityCollection);
			writerPipeline.Write();

			var provider = new MongoFrameworkQueryProvider<WhereIdMatchesStringModel>(connection);
			var queryable = new MongoFrameworkQueryable<WhereIdMatchesStringModel>(provider);
			
			var entityIds = entityCollection.Select(e => e.Id).Take(2);

			var idMatchQueryable = LinqExtensions.WhereIdMatches(queryable, entityIds);

			Assert.AreEqual(2, idMatchQueryable.Count());
			Assert.IsTrue(idMatchQueryable.ToList().All(e => entityIds.Contains(e.Id)));
		}

		[TestMethod]
		public void SearchText()
		{
			var connection = TestConfiguration.GetConnection();
			var dbSet = new MongoDbSet<SearchTextModel>();
			dbSet.SetConnection(connection);

			dbSet.AddRange(new SearchTextModel[]
			{
				new SearchTextModel { MiscField = 1, Text = "The quick brown fox jumps over the lazy dog." },
				new SearchTextModel { MiscField = 2, Text = "The five boxing wizards jump quickly." },
				new SearchTextModel { MiscField = 3, Text = "The quick brown fox jumps over the lazy dog." },
				new SearchTextModel { MiscField = 4, Text = "Jived fox nymph grabs quick waltz." },
			});
			dbSet.SaveChanges();

			Assert.AreEqual(4, dbSet.SearchText("quick").Count());
			Assert.AreEqual(0, dbSet.SearchText("the").Count()); //Stop words aren't used in text indexes: https://docs.mongodb.com/manual/core/index-text/#supported-languages-and-stop-words
			Assert.AreEqual(2, dbSet.SearchText("dog").Count());
			Assert.AreEqual(1, dbSet.SearchText("jived").Count());

			Assert.AreEqual(1, dbSet.SearchText("quick").Where(e => e.MiscField == 3).Count());
		}

		[TestMethod]
		public void SearchNear()
		{
			var connection = TestConfiguration.GetConnection();
			var dbSet = new MongoDbSet<SearchGeoModel>();
			dbSet.SetConnection(connection);

			dbSet.AddRange(new SearchGeoModel[]
			{
				new SearchGeoModel { Description = "New York", PrimaryCoordinates = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
					new GeoJson2DGeographicCoordinates(-74.005974, 40.712776)
				) },
				new SearchGeoModel { Description = "Adelaide", PrimaryCoordinates = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
					new GeoJson2DGeographicCoordinates(138.600739, -34.928497)
				) },
				new SearchGeoModel { Description = "Perth", PrimaryCoordinates = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
					new GeoJson2DGeographicCoordinates(115.860458, -31.950527)
				) },
				new SearchGeoModel { Description = "Hobart", PrimaryCoordinates = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
					new GeoJson2DGeographicCoordinates(147.327194, -42.882137)
				) }
			});
			dbSet.SaveChanges();

			var classMap = MongoDB.Bson.Serialization.BsonClassMap.GetRegisteredClassMaps()
				.Where(cm => cm.ClassType == typeof(SearchGeoModel)).FirstOrDefault();
			var extraElements = classMap.ExtraElementsMemberMap;
			var extraElementsMapIndex = typeof(MongoDB.Bson.Serialization.BsonClassMap).GetProperty("ExtraElementsMemberMapIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(classMap);

			var results = dbSet.SearchGeoNear(e => e.PrimaryCoordinates, new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
				new GeoJson2DGeographicCoordinates(138, 30)
			)).ToArray();

			Assert.AreEqual(4, results.Count());
			Assert.AreEqual(138.600739, results[0].PrimaryCoordinates.Coordinates.Longitude);
			Assert.AreEqual(-34.928497, results[0].PrimaryCoordinates.Coordinates.Latitude);
			Assert.AreEqual(-74.005974, results[3].PrimaryCoordinates.Coordinates.Longitude);
			Assert.AreEqual(40.712776, results[3].PrimaryCoordinates.Coordinates.Latitude);

			Assert.IsTrue(results[0].ExtraElements.ContainsKey("Distance"));
		}
	}
}

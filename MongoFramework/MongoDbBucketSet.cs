﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoFramework.Infrastructure;
using MongoFramework.Infrastructure.Indexing;
using MongoFramework.Infrastructure.Mapping;

namespace MongoFramework
{
	public class MongoDbBucketSet<TGroup, TSubEntity> : IMongoDbSet where TGroup : class where TSubEntity : class
	{
		private IEntityWriter<EntityBucket<TGroup, TSubEntity>> EntityWriter { get; set; }
		private IEntityReader<EntityBucket<TGroup, TSubEntity>> EntityReader { get; set; }
		private IEntityIndexWriter<EntityBucket<TGroup, TSubEntity>> EntityIndexWriter { get; set; }
		private EntityBucketCollection<TGroup, TSubEntity> BucketCollection { get; set; }

		public int BucketSize { get; }

		public MongoDbBucketSet(int bucketSize)
		{
			BucketSize = bucketSize;
		}

		public void SetDatabase(IMongoDatabase database)
		{
			var entityMapper = new EntityMapper<EntityBucket<TGroup, TSubEntity>>();
			EntityWriter = new EntityWriter<EntityBucket<TGroup, TSubEntity>>(database, entityMapper);
			EntityReader = new EntityReader<EntityBucket<TGroup, TSubEntity>>(database, entityMapper);
			
			//TODO: Look at this again in the future, this seems unnecessarily complex 
			var indexMapper = new EntityIndexMapper<EntityBucket<TGroup, TSubEntity>>(entityMapper);
			var collection = database.GetCollection<EntityBucket<TGroup, TSubEntity>>(entityMapper.GetCollectionName());
			EntityIndexWriter = new EntityIndexWriter<EntityBucket<TGroup, TSubEntity>>(collection, indexMapper);

			BucketCollection = new EntityBucketCollection<TGroup, TSubEntity>(EntityReader, BucketSize);
		}

		public virtual void Add(TGroup group, TSubEntity entity)
		{
			if (group == null)
			{
				throw new ArgumentNullException(nameof(group));
			}

			if (entity == null)
			{
				throw new ArgumentNullException(nameof(entity));
			}

			BucketCollection.AddEntity(group, entity);
		}

		public virtual void AddRange(TGroup group, IEnumerable<TSubEntity> entities)
		{
			if (group == null)
			{
				throw new ArgumentNullException(nameof(group));
			}

			if (entities == null)
			{
				throw new ArgumentNullException(nameof(entities));
			}

			foreach (var entity in entities)
			{
				BucketCollection.AddEntity(group, entity);
			}
		}

		public virtual IQueryable<TSubEntity> WithGroup(TGroup group)
		{
			var totalItemCount = EntityReader.AsQueryable().Where(e => e.Group == group).Sum(e => e.ItemCount);
			return EntityReader.AsQueryable().Where(e => e.Group == group).OrderBy(e => e.Index).SelectMany(e => e.Items).Take(totalItemCount);
		}

		public virtual IQueryable<TGroup> Groups()
		{
			return EntityReader.AsQueryable().Select(e => e.Group).Distinct();
		}

		public virtual void SaveChanges()
		{
			EntityIndexWriter.ApplyIndexing();
			var entityCollection = BucketCollection.AsEntityCollection();
			EntityWriter.Write(entityCollection);
			BucketCollection.Clear();
		}

		public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			await EntityIndexWriter.ApplyIndexingAsync();
			cancellationToken.ThrowIfCancellationRequested();
			var entityCollection = BucketCollection.AsEntityCollection();
			await EntityWriter.WriteAsync(entityCollection);
			BucketCollection.Clear();
		}
	}
}

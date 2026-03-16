/*
 Navicat Premium Dump SQL

 Source Server         : ep
 Source Server Type    : PostgreSQL
 Source Server Version : 170008 (170008)
 Source Host           : ep-red-lab-a1vmd8oy-pooler.ap-southeast-1.aws.neon.tech:5432
 Source Catalog        : neondb
 Source Schema         : public

 Target Server Type    : PostgreSQL
 Target Server Version : 170008 (170008)
 File Encoding         : 65001

 Date: 16/03/2026 21:00:31
*/


-- ----------------------------
-- Table structure for Orders
-- ----------------------------
DROP TABLE IF EXISTS "public"."Orders";
CREATE TABLE "public"."Orders" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "ProductId" uuid NOT NULL,
  "PriceId" uuid NOT NULL,
  "SubscriptionId" uuid,
  "OriginalAmount" int8 NOT NULL,
  "ActualAmount" int8 NOT NULL,
  "DiscountAmount" int8 DEFAULT 0,
  "Status" int2 NOT NULL DEFAULT 0,
  "ExpiredTime" timestamptz(6),
  "PaidTime" timestamptz(6),
  "Metadata" jsonb,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "IsDeleted" bool NOT NULL DEFAULT false
)
;

-- ----------------------------
-- Records of Orders
-- ----------------------------
INSERT INTO "public"."Orders" VALUES ('648fa5b9-d4b9-4471-859f-45549427c44c', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:28:25.040951+00', NULL, NULL, '2026-03-10 06:28:25.040999+00', 'f');
INSERT INTO "public"."Orders" VALUES ('e88c9593-1c34-4b49-aa98-a95939acd797', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:28:27.14146+00', NULL, NULL, '2026-03-10 06:28:27.141461+00', 'f');
INSERT INTO "public"."Orders" VALUES ('423422cc-ece9-46fd-9092-9a5661c7755f', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:28:30.973144+00', NULL, NULL, '2026-03-10 06:28:30.973145+00', 'f');
INSERT INTO "public"."Orders" VALUES ('300bf478-df7f-4b9a-af5e-12807f8bd9fa', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 05:43:36.92892+00', NULL, NULL, '2026-03-10 05:43:36.928941+00', 'f');
INSERT INTO "public"."Orders" VALUES ('9a5e5df9-1efd-4f73-80ca-54f2a3b620bc', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 05:45:20.82365+00', NULL, NULL, '2026-03-10 05:45:20.823651+00', 'f');
INSERT INTO "public"."Orders" VALUES ('ed1e3372-0724-4c76-bd14-2956600a51de', '11221872-44f0-48ea-9d2d-587f6c782209', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:02:31.136766+00', NULL, NULL, '2026-03-10 06:02:31.136807+00', 'f');
INSERT INTO "public"."Orders" VALUES ('11857476-3e63-4b65-96fe-1dd9211419a7', '11221872-44f0-48ea-9d2d-587f6c782209', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:02:33.266106+00', NULL, NULL, '2026-03-10 06:02:33.266108+00', 'f');
INSERT INTO "public"."Orders" VALUES ('6d2be81e-c33c-4b7b-b7a6-ce5f1cf636dc', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:02:36.868138+00', NULL, NULL, '2026-03-10 06:02:36.868139+00', 'f');
INSERT INTO "public"."Orders" VALUES ('9d93d98f-4a9c-4c57-bc10-4967c36ab33a', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:08:44.208502+00', NULL, NULL, '2026-03-10 06:08:44.208545+00', 'f');
INSERT INTO "public"."Orders" VALUES ('63f479a3-d21a-4ecf-adb0-8f7daa2d9498', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:09:45.76067+00', NULL, NULL, '2026-03-10 06:09:45.760775+00', 'f');
INSERT INTO "public"."Orders" VALUES ('06bf5957-4206-410e-b74d-bf2df86217e1', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:10:33.251012+00', NULL, NULL, '2026-03-10 06:10:33.251129+00', 'f');
INSERT INTO "public"."Orders" VALUES ('b72f8d3f-74cd-4da7-8752-3d16884a4124', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'c402d010-176b-4578-a2d5-afabbc6ba06e', 8900, 8900, 0, 1, '2026-03-11 06:12:16.229144+00', '2026-03-10 06:12:18.052053+00', NULL, '2026-03-10 06:12:16.229191+00', 'f');
INSERT INTO "public"."Orders" VALUES ('2ed78d6a-89dd-4ce4-8731-eb9e255ca299', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:19:21.750176+00', NULL, NULL, '2026-03-10 06:19:21.750221+00', 'f');
INSERT INTO "public"."Orders" VALUES ('bd46d34c-3591-4f1a-b9fb-68e42ff7b835', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:20:34.902142+00', NULL, NULL, '2026-03-10 06:20:34.902346+00', 'f');
INSERT INTO "public"."Orders" VALUES ('00a578db-f446-4bfb-82f5-70cb3316ca78', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:20:37.092035+00', NULL, NULL, '2026-03-10 06:20:37.092037+00', 'f');
INSERT INTO "public"."Orders" VALUES ('cac68f2c-9bfa-4483-a8c1-eb373f40a2ba', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:20:38.011837+00', NULL, NULL, '2026-03-10 06:20:38.011838+00', 'f');
INSERT INTO "public"."Orders" VALUES ('aed9e193-fcea-45e9-881e-f55ea013530b', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:21:28.634002+00', NULL, NULL, '2026-03-10 06:21:28.634004+00', 'f');
INSERT INTO "public"."Orders" VALUES ('5d6547c0-0fcb-4e93-82e0-601ccbd857b7', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 06:21:29.505561+00', NULL, NULL, '2026-03-10 06:21:29.505562+00', 'f');
INSERT INTO "public"."Orders" VALUES ('4b2dab96-d7e8-4df7-af59-ea81d683e97f', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '28645f58-7e4a-4856-a96b-2d42f17de2da', 8900, 8900, 0, 1, '2026-03-11 06:21:26.532829+00', '2026-03-10 06:21:30.542835+00', NULL, '2026-03-10 06:21:26.533078+00', 'f');
INSERT INTO "public"."Orders" VALUES ('c193db53-6878-4b09-997d-252f78c92ce8', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'e3cab1f0-2287-4f34-a7cb-20f1da94a06b', 8900, 8900, 0, 1, '2026-03-11 06:22:22.753072+00', '2026-03-10 06:22:26.47066+00', NULL, '2026-03-10 06:22:22.753147+00', 'f');
INSERT INTO "public"."Orders" VALUES ('6e4b0f80-0245-4706-8724-1a918c7d8ef4', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:22:24.683148+00', NULL, NULL, '2026-03-10 06:22:24.683151+00', 'f');
INSERT INTO "public"."Orders" VALUES ('c8d5d115-81f6-4fec-ac7a-75c6acf17f57', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:22:25.600226+00', NULL, NULL, '2026-03-10 06:22:25.600228+00', 'f');
INSERT INTO "public"."Orders" VALUES ('70404cf5-1c3d-4001-9c71-affecef4ca97', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'bc137653-b4a1-4dbc-95e3-035ec37933e0', 8900, 8900, 0, 1, '2026-03-11 06:23:20.792928+00', '2026-03-10 06:23:24.892431+00', NULL, '2026-03-10 06:23:20.792971+00', 'f');
INSERT INTO "public"."Orders" VALUES ('3b436ea5-7f45-42ce-9893-205610cd7798', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:23:22.701925+00', NULL, NULL, '2026-03-10 06:23:22.701926+00', 'f');
INSERT INTO "public"."Orders" VALUES ('cce18797-3065-48e1-a966-00f6a2007bf2', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:23:23.640174+00', NULL, NULL, '2026-03-10 06:23:23.640175+00', 'f');
INSERT INTO "public"."Orders" VALUES ('8fa803e3-5213-4e17-b002-a105798b3af9', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '43b1d636-5e10-406a-a540-74418d001fc3', 8900, 8900, 0, 1, '2026-03-11 06:24:47.73322+00', '2026-03-10 06:24:51.462758+00', NULL, '2026-03-10 06:24:47.733266+00', 'f');
INSERT INTO "public"."Orders" VALUES ('5c418196-5cd2-4d12-98f8-b29b62d4276a', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:24:49.665711+00', NULL, NULL, '2026-03-10 06:24:49.665713+00', 'f');
INSERT INTO "public"."Orders" VALUES ('6bfb181f-a8fd-4315-be34-567c348c967a', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:24:50.553207+00', NULL, NULL, '2026-03-10 06:24:50.553209+00', 'f');
INSERT INTO "public"."Orders" VALUES ('f60a0a4c-f775-4acf-8f1a-cd4a9abe886d', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '155c3d4c-288c-482f-8d08-d5962b72c99f', 8900, 8900, 0, 1, '2026-03-11 06:28:35.874102+00', '2026-03-10 06:28:36.869671+00', NULL, '2026-03-10 06:28:35.874104+00', 'f');
INSERT INTO "public"."Orders" VALUES ('43769fcc-1d14-4d15-86f5-6dc0ccd2d5c1', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:28:38.151968+00', NULL, NULL, '2026-03-10 06:28:38.151969+00', 'f');
INSERT INTO "public"."Orders" VALUES ('844c5a66-e640-408b-ab64-5a3b25b9c72d', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-11 06:28:39.566704+00', NULL, NULL, '2026-03-10 06:28:39.566706+00', 'f');
INSERT INTO "public"."Orders" VALUES ('6e74f4e5-c89c-4683-8fcf-486d5ff696c2', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '37cdf429-532f-4f12-9559-70bd6600b349', 8900, 8900, 0, 1, '2026-03-11 07:07:52.969074+00', '2026-03-10 07:08:45.235388+00', NULL, '2026-03-10 07:07:52.969176+00', 'f');
INSERT INTO "public"."Orders" VALUES ('bb7a0c5c-64aa-48ff-a0c0-f8ce2015a836', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 07:17:56.003728+00', NULL, NULL, '2026-03-10 07:17:56.00373+00', 'f');
INSERT INTO "public"."Orders" VALUES ('428fee98-5cd1-4cda-8414-e552ebd1d596', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'a645c0bf-2063-4ae1-a68f-de0124fe68cb', 8900, 8900, 0, 1, '2026-03-11 12:34:58.202822+00', '2026-03-10 12:35:54.044739+00', NULL, '2026-03-10 12:34:58.202872+00', 'f');
INSERT INTO "public"."Orders" VALUES ('37d58a9d-5efd-4d57-bab2-702d90a99ceb', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-11 12:36:50.241777+00', NULL, NULL, '2026-03-10 12:36:50.241779+00', 'f');
INSERT INTO "public"."Orders" VALUES ('2410c92e-a5db-4289-b986-b3efd4824a08', '8377ee31-9908-40ed-b025-ef569bc23b10', 'a81ff2ec-d0b8-488a-afdf-b66efeffa8d2', '60c07c41-bc77-4966-ac49-ae99d1f50b83', '83213719-bdf1-40f7-b0ad-92a6b8b28fb7', 9900, 9900, 0, 1, '2026-03-11 12:41:51.05303+00', '2026-03-10 12:42:11.200855+00', NULL, '2026-03-10 12:41:51.053032+00', 'f');
INSERT INTO "public"."Orders" VALUES ('eafda27c-45e8-4293-89ef-cbf2cde8f5d3', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-12 01:41:11.869904+00', NULL, NULL, '2026-03-11 01:41:11.86999+00', 'f');
INSERT INTO "public"."Orders" VALUES ('b0befc15-bb85-4663-8351-a16af05ead74', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'e9b7836d-ea2e-40dc-8f0a-af5f4a0ef684', 8900, 8900, 0, 1, '2026-03-12 01:42:47.847888+00', '2026-03-11 01:42:50.039295+00', NULL, '2026-03-11 01:42:47.847949+00', 'f');
INSERT INTO "public"."Orders" VALUES ('30afdd94-d161-4d3a-b85f-edf2d68e09b1', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '3981fdd3-ac97-4399-ad99-780935345034', 8900, 8900, 0, 1, '2026-03-12 02:38:03.868979+00', '2026-03-11 02:38:32.808889+00', NULL, '2026-03-11 02:38:03.869042+00', 'f');
INSERT INTO "public"."Orders" VALUES ('42a11dc4-9bec-49b5-a32b-178b66065166', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-12 02:39:44.946384+00', NULL, NULL, '2026-03-11 02:39:44.946386+00', 'f');
INSERT INTO "public"."Orders" VALUES ('ac8087ae-6f95-4f03-a6ce-34f1db9696dd', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-12 02:40:30.862175+00', NULL, NULL, '2026-03-11 02:40:30.862177+00', 'f');
INSERT INTO "public"."Orders" VALUES ('6338a569-4603-4e29-baee-8cfff478f13c', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-13 11:09:33.087478+00', NULL, NULL, '2026-03-12 11:09:33.087519+00', 'f');
INSERT INTO "public"."Orders" VALUES ('871a3c7e-23c4-4483-aeb3-9ac4795dc31d', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-13 11:10:28.353414+00', NULL, NULL, '2026-03-12 11:10:28.353417+00', 'f');
INSERT INTO "public"."Orders" VALUES ('2e882e79-37e3-45f0-8200-04c48cda53e8', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-13 11:14:06.991284+00', NULL, NULL, '2026-03-12 11:14:06.991287+00', 'f');
INSERT INTO "public"."Orders" VALUES ('9279b7a6-cab6-4055-bb85-da9319c1dbd4', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'ecd92cd7-cc11-4bbf-8488-4207a20936de', 8900, 8900, 0, 1, '2026-03-13 11:41:14.557259+00', '2026-03-12 11:41:26.679949+00', NULL, '2026-03-12 11:41:14.557466+00', 'f');
INSERT INTO "public"."Orders" VALUES ('bc5d273d-c39f-47f1-ab85-cdece4c128f7', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '99745637-2611-41f8-9653-42eb9c79d605', 8900, 8900, 0, 1, '2026-03-13 12:06:17.42937+00', '2026-03-12 12:06:37.917413+00', NULL, '2026-03-12 12:06:17.42953+00', 'f');
INSERT INTO "public"."Orders" VALUES ('b38769a2-8618-4fbc-99d7-c3baef8a5089', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-13 12:06:43.489656+00', NULL, NULL, '2026-03-12 12:06:43.489658+00', 'f');
INSERT INTO "public"."Orders" VALUES ('026320d4-1396-42c8-8cf1-ed088256650a', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 2, '2026-03-13 12:08:24.958036+00', NULL, NULL, '2026-03-12 12:08:24.958038+00', 'f');
INSERT INTO "public"."Orders" VALUES ('aca474f3-cacb-4224-819e-64f521abd072', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-13 12:19:51.775319+00', NULL, NULL, '2026-03-12 12:19:51.775321+00', 'f');
INSERT INTO "public"."Orders" VALUES ('78d36eb6-7897-41c8-85fd-35e8e1e12f80', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', NULL, 8900, 8900, 0, 0, '2026-03-13 12:19:58.291559+00', NULL, NULL, '2026-03-12 12:19:58.29156+00', 'f');
INSERT INTO "public"."Orders" VALUES ('9cca65fe-2083-4c56-af96-f1e9d56e66c9', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'f0416006-56f5-42d5-926d-45b04bd3a305', 8900, 8900, 0, 1, '2026-03-16 15:32:00.994804+00', '2026-03-15 15:32:43.725823+00', NULL, '2026-03-15 15:32:00.994829+00', 'f');
INSERT INTO "public"."Orders" VALUES ('b60bbe92-3bfd-4038-b827-0fef6bc1e2b0', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'a068a866-bcef-4c57-8556-e5299e4e007d', 8900, 8900, 0, 1, '2026-03-16 15:37:05.53544+00', '2026-03-15 15:37:10.496592+00', NULL, '2026-03-15 15:37:05.535443+00', 'f');
INSERT INTO "public"."Orders" VALUES ('bdfa7f89-14ba-4a12-88ec-07d499e4aa12', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'eca82015-f546-44d4-b2da-2d0c54d273fa', 8900, 8900, 0, 1, '2026-03-17 12:59:37.168378+00', '2026-03-16 12:59:48.477447+00', NULL, '2026-03-16 12:59:37.168403+00', 'f');

-- ----------------------------
-- Table structure for PaymentMethods
-- ----------------------------
DROP TABLE IF EXISTS "public"."PaymentMethods";
CREATE TABLE "public"."PaymentMethods" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "Type" varchar(30) COLLATE "pg_catalog"."default" NOT NULL,
  "Gateway" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "ExternalId" varchar(255) COLLATE "pg_catalog"."default",
  "IsDefault" bool DEFAULT false,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
)
;

-- ----------------------------
-- Records of PaymentMethods
-- ----------------------------

-- ----------------------------
-- Table structure for Prices
-- ----------------------------
DROP TABLE IF EXISTS "public"."Prices";
CREATE TABLE "public"."Prices" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "ProductId" uuid NOT NULL,
  "Name" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "BillingPeriod" varchar(20) COLLATE "pg_catalog"."default" NOT NULL,
  "Amount" int8 NOT NULL,
  "Currency" varchar(10) COLLATE "pg_catalog"."default" DEFAULT 'USD'::character varying,
  "IsActive" bool DEFAULT true,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
)
;

-- ----------------------------
-- Records of Prices
-- ----------------------------
INSERT INTO "public"."Prices" VALUES ('d50ad152-197b-4173-8e5d-6c51fd0d775c', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'Standard Monthly', 'month', 8900, 'USD', 't', '2026-03-09 12:56:00.305528+00');
INSERT INTO "public"."Prices" VALUES ('60c07c41-bc77-4966-ac49-ae99d1f50b83', 'a81ff2ec-d0b8-488a-afdf-b66efeffa8d2', 'Standard Monthly', 'month', 9900, 'USD', 't', '2026-03-09 12:56:00.305528+00');

-- ----------------------------
-- Table structure for Products
-- ----------------------------
DROP TABLE IF EXISTS "public"."Products";
CREATE TABLE "public"."Products" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "Code" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "Name" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "Description" text COLLATE "pg_catalog"."default",
  "IsActive" bool DEFAULT true,
  "Metadata" jsonb,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
)
;

-- ----------------------------
-- Records of Products
-- ----------------------------
INSERT INTO "public"."Products" VALUES ('22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'CRYPTO_CYCLE_AI', 'CryptoCycleAI', '加密货币周期分析AI工具', 't', NULL, '2026-03-09 12:56:00.305528+00');
INSERT INTO "public"."Products" VALUES ('a81ff2ec-d0b8-488a-afdf-b66efeffa8d2', 'CLINIC_VOICE_AI', 'ClinicVoiceAI', '医疗语音转病历工具', 't', NULL, '2026-03-09 12:56:00.305528+00');

-- ----------------------------
-- Table structure for RefreshTokens
-- ----------------------------
DROP TABLE IF EXISTS "public"."RefreshTokens";
CREATE TABLE "public"."RefreshTokens" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "TokenHash" varchar(256) COLLATE "pg_catalog"."default" NOT NULL,
  "ExpiresAt" timestamptz(6) NOT NULL,
  "Revoked" bool NOT NULL DEFAULT false,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "CreatedBy" uuid
)
;

-- ----------------------------
-- Records of RefreshTokens
-- ----------------------------
INSERT INTO "public"."RefreshTokens" VALUES ('13ff456b-fcec-4549-971f-7cd8d546f4cd', '11221872-44f0-48ea-9d2d-587f6c782209', 'b0be73f0fe64fa898c7713a315c3e5aeaf690cefdf07bf2e0a0528b65946f365', '2026-03-16 12:59:10.618724+00', 'f', '2026-03-09 12:59:10.618938+00', '11221872-44f0-48ea-9d2d-587f6c782209');
INSERT INTO "public"."RefreshTokens" VALUES ('2941b403-1864-4d55-8daa-bf7463f2e272', '8377ee31-9908-40ed-b025-ef569bc23b10', '2a5fa1bd30eb2aee45adb9f8d0bd9a7565d0d0dcac2acd668601d9a632f16454', '2026-03-17 03:22:00.213349+00', 'f', '2026-03-10 03:22:00.213405+00', '8377ee31-9908-40ed-b025-ef569bc23b10');
INSERT INTO "public"."RefreshTokens" VALUES ('ba7242d6-a7cb-4564-9f5c-45950c1fee85', 'c875b210-a576-49f0-9b9a-3e2e9a011ecf', 'fd559ef4e9f9b718721f9a41663932ccbc65afaa0939c2b343aaa1169f4c3fc4', '2026-03-22 12:57:44.099899+00', 'f', '2026-03-15 12:57:44.09993+00', 'c875b210-a576-49f0-9b9a-3e2e9a011ecf');

-- ----------------------------
-- Table structure for Roles
-- ----------------------------
DROP TABLE IF EXISTS "public"."Roles";
CREATE TABLE "public"."Roles" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "Name" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "Code" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "Description" varchar(255) COLLATE "pg_catalog"."default",
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP
)
;

-- ----------------------------
-- Records of Roles
-- ----------------------------
INSERT INTO "public"."Roles" VALUES ('d6e5964c-01bb-4108-b8ae-accc935419a3', '超级管理员', 'SUPER_ADMIN', '最高权限：系统级配置与全数据访问', '2026-03-09 12:56:00.305528+00');
INSERT INTO "public"."Roles" VALUES ('3303a680-4a23-420e-b124-49346969faa2', '管理员', 'ADMIN', '管理权限：管理普通用户及查看业务统计', '2026-03-09 12:56:00.305528+00');
INSERT INTO "public"."Roles" VALUES ('9ded3787-b9e1-4689-ad86-2a51da8789a7', '普通用户', 'USER', '基础权限：仅操作个人订阅与数据', '2026-03-09 12:56:00.305528+00');

-- ----------------------------
-- Table structure for Subscriptions
-- ----------------------------
DROP TABLE IF EXISTS "public"."Subscriptions";
CREATE TABLE "public"."Subscriptions" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "ProductId" uuid NOT NULL,
  "PriceId" uuid NOT NULL,
  "OrderId" uuid,
  "Status" int2 NOT NULL DEFAULT 0,
  "StartDate" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "EndDate" timestamptz(6),
  "AutoRenew" bool NOT NULL DEFAULT true,
  "Metadata" jsonb,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "IsDeleted" bool NOT NULL DEFAULT false
)
;

-- ----------------------------
-- Records of Subscriptions
-- ----------------------------
INSERT INTO "public"."Subscriptions" VALUES ('c402d010-176b-4578-a2d5-afabbc6ba06e', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'b72f8d3f-74cd-4da7-8752-3d16884a4124', 1, '2026-03-10 06:12:18.279431+00', '2026-04-10 06:12:18.279553+00', 't', NULL, '2026-03-10 06:12:18.279657+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('28645f58-7e4a-4856-a96b-2d42f17de2da', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '4b2dab96-d7e8-4df7-af59-ea81d683e97f', 1, '2026-03-10 06:21:30.69436+00', '2026-04-10 06:21:30.694601+00', 't', NULL, '2026-03-10 06:21:30.694766+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('e3cab1f0-2287-4f34-a7cb-20f1da94a06b', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'c193db53-6878-4b09-997d-252f78c92ce8', 1, '2026-03-10 06:22:26.641244+00', '2026-04-10 06:22:26.641369+00', 't', NULL, '2026-03-10 06:22:26.64146+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('bc137653-b4a1-4dbc-95e3-035ec37933e0', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '70404cf5-1c3d-4001-9c71-affecef4ca97', 1, '2026-03-10 06:23:25.061099+00', '2026-04-10 06:23:25.061224+00', 't', NULL, '2026-03-10 06:23:25.061328+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('43b1d636-5e10-406a-a540-74418d001fc3', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '8fa803e3-5213-4e17-b002-a105798b3af9', 1, '2026-03-10 06:24:51.629691+00', '2026-04-10 06:24:51.629818+00', 't', NULL, '2026-03-10 06:24:51.629928+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('155c3d4c-288c-482f-8d08-d5962b72c99f', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'f60a0a4c-f775-4acf-8f1a-cd4a9abe886d', 1, '2026-03-10 06:28:37.039082+00', '2026-04-10 06:28:37.039204+00', 't', NULL, '2026-03-10 06:28:37.039299+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('37cdf429-532f-4f12-9559-70bd6600b349', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '6e74f4e5-c89c-4683-8fcf-486d5ff696c2', 1, '2026-03-10 07:08:45.431148+00', '2026-04-10 07:08:45.431436+00', 't', NULL, '2026-03-10 07:08:45.431847+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('a645c0bf-2063-4ae1-a68f-de0124fe68cb', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '428fee98-5cd1-4cda-8414-e552ebd1d596', 1, '2026-03-10 12:35:54.223079+00', '2026-04-10 12:35:54.223268+00', 't', NULL, '2026-03-10 12:35:54.223554+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('83213719-bdf1-40f7-b0ad-92a6b8b28fb7', '8377ee31-9908-40ed-b025-ef569bc23b10', 'a81ff2ec-d0b8-488a-afdf-b66efeffa8d2', '60c07c41-bc77-4966-ac49-ae99d1f50b83', '2410c92e-a5db-4289-b986-b3efd4824a08', 1, '2026-03-10 12:42:11.368983+00', '2026-04-10 12:42:11.368985+00', 't', NULL, '2026-03-10 12:42:11.368994+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('e9b7836d-ea2e-40dc-8f0a-af5f4a0ef684', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'b0befc15-bb85-4663-8351-a16af05ead74', 1, '2026-03-11 01:42:50.21964+00', '2026-04-11 01:42:50.219763+00', 't', NULL, '2026-03-11 01:42:50.21981+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('3981fdd3-ac97-4399-ad99-780935345034', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '30afdd94-d161-4d3a-b85f-edf2d68e09b1', 1, '2026-03-11 02:38:32.983697+00', '2026-04-11 02:38:32.983933+00', 't', NULL, '2026-03-11 02:38:32.984173+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('ecd92cd7-cc11-4bbf-8488-4207a20936de', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '9279b7a6-cab6-4055-bb85-da9319c1dbd4', 1, '2026-03-12 11:41:26.857243+00', '2026-04-12 11:41:26.857557+00', 't', NULL, '2026-03-12 11:41:26.857762+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('99745637-2611-41f8-9653-42eb9c79d605', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'bc5d273d-c39f-47f1-ab85-cdece4c128f7', 1, '2026-03-12 12:06:38.099391+00', '2026-04-12 12:06:38.099666+00', 't', NULL, '2026-03-12 12:06:38.099858+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('f0416006-56f5-42d5-926d-45b04bd3a305', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', '9cca65fe-2083-4c56-af96-f1e9d56e66c9', 1, '2026-03-15 15:32:43.811876+00', '2026-04-15 15:32:43.812028+00', 't', NULL, '2026-03-15 15:32:43.812071+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('a068a866-bcef-4c57-8556-e5299e4e007d', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'b60bbe92-3bfd-4038-b827-0fef6bc1e2b0', 1, '2026-03-15 15:37:10.562348+00', '2026-04-15 15:37:10.562351+00', 't', NULL, '2026-03-15 15:37:10.562358+00', 'f');
INSERT INTO "public"."Subscriptions" VALUES ('eca82015-f546-44d4-b2da-2d0c54d273fa', '8377ee31-9908-40ed-b025-ef569bc23b10', '22f8f698-5a6d-4e4b-b404-d9b94cb8732d', 'd50ad152-197b-4173-8e5d-6c51fd0d775c', 'bdfa7f89-14ba-4a12-88ec-07d499e4aa12', 1, '2026-03-16 12:59:48.556556+00', '2026-04-16 12:59:48.556735+00', 't', NULL, '2026-03-16 12:59:48.556819+00', 'f');

-- ----------------------------
-- Table structure for Transactions
-- ----------------------------
DROP TABLE IF EXISTS "public"."Transactions";
CREATE TABLE "public"."Transactions" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "OrderId" uuid NOT NULL,
  "SubscriptionId" uuid,
  "Amount" int8 NOT NULL,
  "Currency" varchar(10) COLLATE "pg_catalog"."default" DEFAULT 'USD'::character varying,
  "Gateway" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "ExternalTransactionId" varchar(255) COLLATE "pg_catalog"."default",
  "Status" int2 NOT NULL DEFAULT 0,
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "Remarks" text COLLATE "pg_catalog"."default"
)
;

-- ----------------------------
-- Records of Transactions
-- ----------------------------
INSERT INTO "public"."Transactions" VALUES ('f3835abe-bd99-4776-9360-04d41938b3e7', '8377ee31-9908-40ed-b025-ef569bc23b10', 'b72f8d3f-74cd-4da7-8752-3d16884a4124', 'c402d010-176b-4578-a2d5-afabbc6ba06e', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:12:18.38199+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('dbebcc61-07b0-4630-a0ba-a7b37dd1bc92', '8377ee31-9908-40ed-b025-ef569bc23b10', '4b2dab96-d7e8-4df7-af59-ea81d683e97f', '28645f58-7e4a-4856-a96b-2d42f17de2da', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:21:30.794499+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('9e55debc-69a7-46aa-a2fd-a3695d2f57a5', '8377ee31-9908-40ed-b025-ef569bc23b10', 'c193db53-6878-4b09-997d-252f78c92ce8', 'e3cab1f0-2287-4f34-a7cb-20f1da94a06b', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:22:26.745477+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('84969c87-6a52-4566-8787-b1d73a21959b', '8377ee31-9908-40ed-b025-ef569bc23b10', 'c8d5d115-81f6-4fec-ac7a-75c6acf17f57', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-10 06:22:27.403786+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('b9b717be-ec4c-49a7-8f11-74b72162ed6b', '8377ee31-9908-40ed-b025-ef569bc23b10', '70404cf5-1c3d-4001-9c71-affecef4ca97', 'bc137653-b4a1-4dbc-95e3-035ec37933e0', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:23:25.164967+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('30e489d3-6294-4ab9-b36d-d3ab2777878a', '8377ee31-9908-40ed-b025-ef569bc23b10', 'cce18797-3065-48e1-a966-00f6a2007bf2', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-10 06:23:25.814698+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('4bffec50-a2ad-4407-9e44-b63e28b85afa', '8377ee31-9908-40ed-b025-ef569bc23b10', '8fa803e3-5213-4e17-b002-a105798b3af9', '43b1d636-5e10-406a-a540-74418d001fc3', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:24:51.730255+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('b06bf3bf-398a-4b11-b91f-9cbec4f02111', '8377ee31-9908-40ed-b025-ef569bc23b10', '6bfb181f-a8fd-4315-be34-567c348c967a', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-10 06:24:52.345528+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('aa5695a9-d55e-4922-9363-d882ee10a351', '8377ee31-9908-40ed-b025-ef569bc23b10', 'f60a0a4c-f775-4acf-8f1a-cd4a9abe886d', '155c3d4c-288c-482f-8d08-d5962b72c99f', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-10 06:28:37.140299+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('c411591e-dacf-45e4-9c70-aecf0975e67d', '8377ee31-9908-40ed-b025-ef569bc23b10', '844c5a66-e640-408b-ab64-5a3b25b9c72d', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-10 06:28:40.413976+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('05842404-18ea-4422-8d55-ebf8a98c96e0', '8377ee31-9908-40ed-b025-ef569bc23b10', '6e74f4e5-c89c-4683-8fcf-486d5ff696c2', '37cdf429-532f-4f12-9559-70bd6600b349', 8900, 'usd', 'Stripe', 'cs_test_a19lFrEBMYvqRcm7AWJjhZt2chvXQEcXQ5JMeUEm0qKU7uEIV7pNEwpMqO', 1, '2026-03-10 07:08:45.559766+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('b63ece02-ae8f-42c3-97ef-55ecd83359cd', '8377ee31-9908-40ed-b025-ef569bc23b10', '428fee98-5cd1-4cda-8414-e552ebd1d596', 'a645c0bf-2063-4ae1-a68f-de0124fe68cb', 8900, 'usd', 'Stripe', 'cs_test_a1qDW65VgGbghbwVhFto1kL7fj1DFYkthjyadQsk8miGiephRJtnsM2uLC', 1, '2026-03-10 12:35:54.347174+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('29f3c2dc-d0d8-4eec-9869-f4630f16f4f1', '8377ee31-9908-40ed-b025-ef569bc23b10', '2410c92e-a5db-4289-b986-b3efd4824a08', '83213719-bdf1-40f7-b0ad-92a6b8b28fb7', 9900, 'usd', 'Stripe', 'cs_test_a1nzWgDi6xJI28J1cYLDQjnYithIovQ0ZXMFdBz1e1PHatzqNLiiXbQGg1', 1, '2026-03-10 12:42:11.452181+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('18cb15dc-996d-4470-b53f-977d2ff30105', '8377ee31-9908-40ed-b025-ef569bc23b10', 'b0befc15-bb85-4663-8351-a16af05ead74', 'e9b7836d-ea2e-40dc-8f0a-af5f4a0ef684', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-11 01:42:50.346205+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('5b8d3db7-f034-4b92-a3e0-bc398fe09674', '8377ee31-9908-40ed-b025-ef569bc23b10', '30afdd94-d161-4d3a-b85f-edf2d68e09b1', '3981fdd3-ac97-4399-ad99-780935345034', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-11 02:38:33.101879+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('e2d38426-40c0-40e5-b02f-da13ce45aefc', '8377ee31-9908-40ed-b025-ef569bc23b10', 'ac8087ae-6f95-4f03-a6ce-34f1db9696dd', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-11 02:40:44.033866+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('b3cdacdb-c30d-477a-b41b-c835ea0f7fbe', '8377ee31-9908-40ed-b025-ef569bc23b10', '9279b7a6-cab6-4055-bb85-da9319c1dbd4', 'ecd92cd7-cc11-4bbf-8488-4207a20936de', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-12 11:41:26.982831+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('6eb2912a-7aee-483d-bbe9-4c22c2f24b20', '8377ee31-9908-40ed-b025-ef569bc23b10', 'bc5d273d-c39f-47f1-ab85-cdece4c128f7', '99745637-2611-41f8-9653-42eb9c79d605', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-12 12:06:38.235875+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('c147acdf-88dd-4b4d-88b2-6eb94444a922', '8377ee31-9908-40ed-b025-ef569bc23b10', '026320d4-1396-42c8-8cf1-ed088256650a', NULL, 8900, 'usd', 'Stripe', 'pi_test_failed', 2, '2026-03-12 12:09:06.929233+00', 'Stripe payment failed: card_declined (test)');
INSERT INTO "public"."Transactions" VALUES ('698aeea6-def1-4d98-8e90-ae2c0b7e8c4e', '8377ee31-9908-40ed-b025-ef569bc23b10', '9cca65fe-2083-4c56-af96-f1e9d56e66c9', 'f0416006-56f5-42d5-926d-45b04bd3a305', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-15 15:32:43.885029+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('148f4202-3ab4-4cf3-9830-0385ae981bc9', '8377ee31-9908-40ed-b025-ef569bc23b10', 'b60bbe92-3bfd-4038-b827-0fef6bc1e2b0', 'a068a866-bcef-4c57-8556-e5299e4e007d', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-15 15:37:10.595421+00', 'Stripe checkout.session.completed');
INSERT INTO "public"."Transactions" VALUES ('3c2a7dab-9d35-4430-a719-b85f12d1d4af', '8377ee31-9908-40ed-b025-ef569bc23b10', 'bdfa7f89-14ba-4a12-88ec-07d499e4aa12', 'eca82015-f546-44d4-b2da-2d0c54d273fa', 8900, 'usd', 'Stripe', 'pi_test_completed', 1, '2026-03-16 12:59:48.618043+00', 'Stripe checkout.session.completed');

-- ----------------------------
-- Table structure for UserRoles
-- ----------------------------
DROP TABLE IF EXISTS "public"."UserRoles";
CREATE TABLE "public"."UserRoles" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserId" uuid NOT NULL,
  "RoleId" uuid NOT NULL,
  "CreatedTime" timestamptz(6) DEFAULT CURRENT_TIMESTAMP
)
;

-- ----------------------------
-- Records of UserRoles
-- ----------------------------
INSERT INTO "public"."UserRoles" VALUES ('e4d88264-ca19-497a-af22-7fa76e44b4f0', '11221872-44f0-48ea-9d2d-587f6c782209', 'd6e5964c-01bb-4108-b8ae-accc935419a3', '2026-03-09 13:03:16.448469+00');

-- ----------------------------
-- Table structure for Users
-- ----------------------------
DROP TABLE IF EXISTS "public"."Users";
CREATE TABLE "public"."Users" (
  "Id" uuid NOT NULL DEFAULT uuid_generate_v4(),
  "UserName" varchar(50) COLLATE "pg_catalog"."default" NOT NULL,
  "PasswordHash" varchar(256) COLLATE "pg_catalog"."default" NOT NULL,
  "Email" varchar(100) COLLATE "pg_catalog"."default" NOT NULL,
  "PhoneNumber" varchar(20) COLLATE "pg_catalog"."default",
  "Status" int2 NOT NULL DEFAULT 1,
  "LastLoginTime" timestamptz(6),
  "CreatedTime" timestamptz(6) NOT NULL DEFAULT CURRENT_TIMESTAMP,
  "UpdatedTime" timestamptz(6),
  "IsDeleted" bool NOT NULL DEFAULT false
)
;

-- ----------------------------
-- Records of Users
-- ----------------------------
INSERT INTO "public"."Users" VALUES ('11221872-44f0-48ea-9d2d-587f6c782209', 'test', '$2a$11$1rts5PSirodY/M10IravceC3GgBxPzNGsN2VeFUsMVkEPGNSkpnzS', 'test@126.com', NULL, 1, NULL, '2026-03-09 12:59:09.553072+00', NULL, 'f');
INSERT INTO "public"."Users" VALUES ('8377ee31-9908-40ed-b025-ef569bc23b10', 'test01', '$2a$11$aMcYjCZ2G2OvvHXe98EiG.N3Ln8Os.z9S38izLeIIbo3321li4oxe', 'test01@126.com', NULL, 1, NULL, '2026-03-10 03:21:59.805952+00', NULL, 'f');
INSERT INTO "public"."Users" VALUES ('c875b210-a576-49f0-9b9a-3e2e9a011ecf', 'test02', '$2a$11$4/KQaC/udTreSBizJEGvJOIVq0XbNX4GBqlS2Z0RoPaBB8Zh2tvVu', 'test02@126.com', NULL, 1, NULL, '2026-03-15 12:57:43.78263+00', NULL, 'f');

-- ----------------------------
-- Table structure for __EFMigrationsHistory
-- ----------------------------
DROP TABLE IF EXISTS "public"."__EFMigrationsHistory";
CREATE TABLE "public"."__EFMigrationsHistory" (
  "MigrationId" varchar(150) COLLATE "pg_catalog"."default" NOT NULL,
  "ProductVersion" varchar(32) COLLATE "pg_catalog"."default" NOT NULL
)
;

-- ----------------------------
-- Records of __EFMigrationsHistory
-- ----------------------------
INSERT INTO "public"."__EFMigrationsHistory" VALUES ('20260303072819_InitialCreate', '10.0.0');

-- ----------------------------
-- Function structure for uuid_generate_v1
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_generate_v1"();
CREATE FUNCTION "public"."uuid_generate_v1"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_generate_v1'
  LANGUAGE c VOLATILE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_generate_v1mc
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_generate_v1mc"();
CREATE FUNCTION "public"."uuid_generate_v1mc"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_generate_v1mc'
  LANGUAGE c VOLATILE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_generate_v3
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_generate_v3"("namespace" uuid, "name" text);
CREATE FUNCTION "public"."uuid_generate_v3"("namespace" uuid, "name" text)
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_generate_v3'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_generate_v4
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_generate_v4"();
CREATE FUNCTION "public"."uuid_generate_v4"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_generate_v4'
  LANGUAGE c VOLATILE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_generate_v5
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_generate_v5"("namespace" uuid, "name" text);
CREATE FUNCTION "public"."uuid_generate_v5"("namespace" uuid, "name" text)
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_generate_v5'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_nil
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_nil"();
CREATE FUNCTION "public"."uuid_nil"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_nil'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_ns_dns
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_ns_dns"();
CREATE FUNCTION "public"."uuid_ns_dns"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_ns_dns'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_ns_oid
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_ns_oid"();
CREATE FUNCTION "public"."uuid_ns_oid"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_ns_oid'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_ns_url
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_ns_url"();
CREATE FUNCTION "public"."uuid_ns_url"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_ns_url'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Function structure for uuid_ns_x500
-- ----------------------------
DROP FUNCTION IF EXISTS "public"."uuid_ns_x500"();
CREATE FUNCTION "public"."uuid_ns_x500"()
  RETURNS "pg_catalog"."uuid" AS '$libdir/uuid-ossp', 'uuid_ns_x500'
  LANGUAGE c IMMUTABLE STRICT
  COST 1;

-- ----------------------------
-- Indexes structure for table Orders
-- ----------------------------
CREATE INDEX "IX_Orders_UserId_Status" ON "public"."Orders" USING btree (
  "UserId" "pg_catalog"."uuid_ops" ASC NULLS LAST,
  "Status" "pg_catalog"."int2_ops" ASC NULLS LAST
);

-- ----------------------------
-- Primary Key structure for table Orders
-- ----------------------------
ALTER TABLE "public"."Orders" ADD CONSTRAINT "Orders_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Primary Key structure for table PaymentMethods
-- ----------------------------
ALTER TABLE "public"."PaymentMethods" ADD CONSTRAINT "PaymentMethods_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Primary Key structure for table Prices
-- ----------------------------
ALTER TABLE "public"."Prices" ADD CONSTRAINT "Prices_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Uniques structure for table Products
-- ----------------------------
ALTER TABLE "public"."Products" ADD CONSTRAINT "Products_Code_key" UNIQUE ("Code");

-- ----------------------------
-- Primary Key structure for table Products
-- ----------------------------
ALTER TABLE "public"."Products" ADD CONSTRAINT "Products_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Indexes structure for table RefreshTokens
-- ----------------------------
CREATE INDEX "IX_RefreshTokens_TokenHash" ON "public"."RefreshTokens" USING btree (
  "TokenHash" COLLATE "pg_catalog"."default" "pg_catalog"."text_ops" ASC NULLS LAST
);
CREATE INDEX "IX_RefreshTokens_UserId" ON "public"."RefreshTokens" USING btree (
  "UserId" "pg_catalog"."uuid_ops" ASC NULLS LAST
);

-- ----------------------------
-- Primary Key structure for table RefreshTokens
-- ----------------------------
ALTER TABLE "public"."RefreshTokens" ADD CONSTRAINT "RefreshTokens_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Uniques structure for table Roles
-- ----------------------------
ALTER TABLE "public"."Roles" ADD CONSTRAINT "Roles_Name_key" UNIQUE ("Name");
ALTER TABLE "public"."Roles" ADD CONSTRAINT "Roles_Code_key" UNIQUE ("Code");

-- ----------------------------
-- Primary Key structure for table Roles
-- ----------------------------
ALTER TABLE "public"."Roles" ADD CONSTRAINT "Roles_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Indexes structure for table Subscriptions
-- ----------------------------
CREATE INDEX "IX_Subscriptions_UserId_Status" ON "public"."Subscriptions" USING btree (
  "UserId" "pg_catalog"."uuid_ops" ASC NULLS LAST,
  "Status" "pg_catalog"."int2_ops" ASC NULLS LAST
);

-- ----------------------------
-- Primary Key structure for table Subscriptions
-- ----------------------------
ALTER TABLE "public"."Subscriptions" ADD CONSTRAINT "Subscriptions_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Indexes structure for table Transactions
-- ----------------------------
CREATE INDEX "IX_Transactions_OrderId_Status" ON "public"."Transactions" USING btree (
  "OrderId" "pg_catalog"."uuid_ops" ASC NULLS LAST,
  "Status" "pg_catalog"."int2_ops" ASC NULLS LAST
);

-- ----------------------------
-- Primary Key structure for table Transactions
-- ----------------------------
ALTER TABLE "public"."Transactions" ADD CONSTRAINT "Transactions_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Uniques structure for table UserRoles
-- ----------------------------
ALTER TABLE "public"."UserRoles" ADD CONSTRAINT "UQ_UserRoles_UserId_RoleId" UNIQUE ("UserId", "RoleId");

-- ----------------------------
-- Primary Key structure for table UserRoles
-- ----------------------------
ALTER TABLE "public"."UserRoles" ADD CONSTRAINT "UserRoles_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Uniques structure for table Users
-- ----------------------------
ALTER TABLE "public"."Users" ADD CONSTRAINT "Users_UserName_key" UNIQUE ("UserName");
ALTER TABLE "public"."Users" ADD CONSTRAINT "Users_Email_key" UNIQUE ("Email");

-- ----------------------------
-- Primary Key structure for table Users
-- ----------------------------
ALTER TABLE "public"."Users" ADD CONSTRAINT "Users_pkey" PRIMARY KEY ("Id");

-- ----------------------------
-- Primary Key structure for table __EFMigrationsHistory
-- ----------------------------
ALTER TABLE "public"."__EFMigrationsHistory" ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");

-- ----------------------------
-- Foreign Keys structure for table Orders
-- ----------------------------
ALTER TABLE "public"."Orders" ADD CONSTRAINT "FK_Orders_Subscriptions" FOREIGN KEY ("SubscriptionId") REFERENCES "public"."Subscriptions" ("Id") ON DELETE SET NULL ON UPDATE NO ACTION;
ALTER TABLE "public"."Orders" ADD CONSTRAINT "Orders_PriceId_fkey" FOREIGN KEY ("PriceId") REFERENCES "public"."Prices" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Orders" ADD CONSTRAINT "Orders_ProductId_fkey" FOREIGN KEY ("ProductId") REFERENCES "public"."Products" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Orders" ADD CONSTRAINT "Orders_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table PaymentMethods
-- ----------------------------
ALTER TABLE "public"."PaymentMethods" ADD CONSTRAINT "PaymentMethods_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table Prices
-- ----------------------------
ALTER TABLE "public"."Prices" ADD CONSTRAINT "Prices_ProductId_fkey" FOREIGN KEY ("ProductId") REFERENCES "public"."Products" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table RefreshTokens
-- ----------------------------
ALTER TABLE "public"."RefreshTokens" ADD CONSTRAINT "RefreshTokens_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table Subscriptions
-- ----------------------------
ALTER TABLE "public"."Subscriptions" ADD CONSTRAINT "Subscriptions_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "public"."Orders" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Subscriptions" ADD CONSTRAINT "Subscriptions_PriceId_fkey" FOREIGN KEY ("PriceId") REFERENCES "public"."Prices" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Subscriptions" ADD CONSTRAINT "Subscriptions_ProductId_fkey" FOREIGN KEY ("ProductId") REFERENCES "public"."Products" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Subscriptions" ADD CONSTRAINT "Subscriptions_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table Transactions
-- ----------------------------
ALTER TABLE "public"."Transactions" ADD CONSTRAINT "Transactions_OrderId_fkey" FOREIGN KEY ("OrderId") REFERENCES "public"."Orders" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Transactions" ADD CONSTRAINT "Transactions_SubscriptionId_fkey" FOREIGN KEY ("SubscriptionId") REFERENCES "public"."Subscriptions" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;
ALTER TABLE "public"."Transactions" ADD CONSTRAINT "Transactions_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE NO ACTION ON UPDATE NO ACTION;

-- ----------------------------
-- Foreign Keys structure for table UserRoles
-- ----------------------------
ALTER TABLE "public"."UserRoles" ADD CONSTRAINT "UserRoles_RoleId_fkey" FOREIGN KEY ("RoleId") REFERENCES "public"."Roles" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION;
ALTER TABLE "public"."UserRoles" ADD CONSTRAINT "UserRoles_UserId_fkey" FOREIGN KEY ("UserId") REFERENCES "public"."Users" ("Id") ON DELETE CASCADE ON UPDATE NO ACTION;

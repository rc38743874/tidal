USE BigBasketball;
GO

SELECT sobj.name,	
(SELECT count(is_selected) FROM sys.sql_dependencies AS sis WHERE sobj.object_id = sis.object_id AND is_selected=1) AS performsSelectCount,
(SELECT count(is_updated) FROM sys.sql_dependencies AS siu WHERE sobj.object_id = siu.object_id AND is_updated=1) AS performsUpdateCount
FROM sys.objects AS sobj WHERE type='P' AND name like '%_X_%';

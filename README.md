# YOSE-Image-Server
Reverse Image Search Server

# YOSE-Reverse-Image-Search
.NET library to perform reverse image search in very high performance with good accuracy.

# Introduction
Reverse image search comes in many ways. In this project there are two types implemented:

Visual Copies. Image files which are visually identical. Using Phash as the algorithm to perform this matching.

Visual Similarity. Visually similar files, using CEDD and FTHC algorithm we are able to find visually similar files.

# Vectors
The vectors are very small which makes it possible to load them into RAM for very fast matching.
The vectors are stored in a RocksDB database and loaded into
RAM when starting.

# Performance
Performance is huge. Even when having 100 of millions of images in the database, the matching time is less than a second. Using tree structures we are able to scale performance as the index grows.

When more than 100 million images in the index, the matching is parallel, the reason is that below that, the matching is so fast so starting a parallel for loop is taking more time than the matching procedure.

# Using the code
Download the code and compile it. Either using it as a separate server outside your application or embed it into your existing application. Start with adding images to the index. When finished query the database either using the hash value of the file or a byte array of the picture.
The server is using a system of hash value of the + frame index when the image comes from
a video, if the picture comes from a bitmap, the index is -1.

This makes it possible to add video frames to the index to search for similar frames inside a video. When querying the database, it tries to load the image vectors from
the database before calculating the image vectors to save time. If not found, it will calculate the vectors. After the vectors has been obtained they will
be matched against the visual copies index using PHash and against the visual similarity index using FTHC algorithm.

# Presenting the result
Start with presenting the visual copies result. They are likely the same image, scaled down versions of the same or
other file formats.

The visual similarity gives a similar score. Sort by similarity and present them as long as you like. Typically 85-90% similarity and above is relevant to show, but it depends on your use case of how many false matches you want to present.

#cbir, #reverseimagesearch #visualsimilarity #phash #CEDD #FTHC #net

## Overview

This project implements a surface clustering algorithm for 3D models using directional distributions in the Unity game engine. The goal is to analyze the impact of different directional distribution models on the quality and performance of surface clustering, which can potentially be used to optimize real-time rendering in computer graphics.

### Abstract

The project explores the use of directional distributions to improve surface clustering for potential optimization of real-time rendering in computer graphics. It implements a modified spherical k-means algorithm in Unity, incorporating three directional distribution models: von Mises-Fisher, Bingham, and Kent. The implementation utilizes C# and the MathNet.Numerics library for mathematical calculations.

The research was conducted on a set of 3D models with varying geometric complexity, including basic shapes like spheres and platonic solids, as well as more complex models like the Stanford Bunny, a pumpkin, and a teddy bear. The quality of clustering was evaluated using the coefficient of variation (CV) of cosine similarity, CV of cluster parameters, and visual inspection of clustered triangles on the models. The performance of the algorithms was assessed based on the execution time of individual algorithm stages and the number of iterations.

The results demonstrate that the choice of directional distribution has a significant impact on both the quality and performance of surface clustering. The algorithms incorporating directional distributions (vMF, Bingham, Kent) generally achieved better clustering quality compared to the standard spherical k-means algorithm, but at the cost of increased execution time, especially for models with a high number of triangles and clusters.

### Key Features

* **Directional distribution-based clustering:** Implements spherical k-means clustering using von Mises-Fisher, Bingham, and Kent distributions.
* **Coherent cluster generation:** Ensures that triangles within a cluster are connected, improving visual consistency.
* **Performance analysis:** Measures execution times for different algorithm stages and the number of iterations.
* **Visualization:** Color-codes triangles based on cluster assignments for visual evaluation of clustering quality.
* **Unity integration:** Utilizes Unity's rendering capabilities and tools for visualization and analysis.
* **External library integration:** Leverages the MathNet.Numerics library for efficient mathematical computations.

## Installation

1. Download the repository as a ZIP file.
2. Extract the ZIP file to a desired location.
3. Add the extracted folder to Unity Hub.
4. Open the project in Unity.
5. In the Project window, navigate to the "Scenes" folder.
6. Double-click the "MainScene" file to load the scene.

## Usage

The user interface provides controls for:
* Importing 3D models.
* Selecting the clustering algorithm (Spherical K-Means, vMF, Bingham, Kent).
* Specifying the number of clusters.
* Viewing logs from the clustering process.
* Toggling the visibility of the wireframe for the imported model.

The application allows users to experiment with different clustering algorithms and parameters to analyze their impact on the quality and performance of surface clustering.

## Requirements

* Unity game engine (version 2022.3.34f1 or later)

## License

MIT License

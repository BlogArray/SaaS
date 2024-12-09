# BlogArray

Welcome to BlogArray! 🌟 Your open-source Headless CMS platform built with ASP.NET Core and Angular, designed to make creating personal blogs and websites effortless and enjoyable. 

## Features

🚀 **Search Engine Optimized**: Boost your visibility and climb the search engine rankings with our built-in SEO tools.

👥 **User Management**: Easily manage user roles and permissions, ensuring the right people have the right access.

📝 **Posts & Pages**: Create, edit, and organize your content with our intuitive post and page management system.

🏷️ **Categories & Tags**: Keep your content organized and discoverable with powerful categorization and tagging features.

🖼️ **Media Management**: Upload, store, and manage your images, videos, and documents with ease.

🎛️ **Powerful Admin Panel**: Manage your site effortlessly with our feature-rich admin dashboard, including built-in analytics.

🧭 **Menu Builder**: Craft the perfect navigation experience for your visitors with our drag-and-drop menu creator.

🔒 **Own Your Data**: Your content, your rules. Host BlogArray on your own servers and maintain full control of your data.

🆓 **Freedom**: Open-source means you're free to modify and adapt BlogArray to fit your unique needs.

💬 **Built-in Comments** (Coming Soon): Foster community engagement with our native commenting system.

📊 **Custom Forms** (Coming Soon): Create surveys, contact forms, and more to interact with your audience.

🎨 **Themes** (Coming Soon): Customize your site's look and feel with our extensible theme.

## Installation

To get started with BlogArray, follow these simple steps:

1. **Prerequisites**: 
   - Ensure you have .NET 8.0 SDK or later installed
   - Install Node.js (version 14 or later) and npm

2. **Clone the Repository:**

    ```bash
    git clone https://github.com/BlogArray/BlogArray.git
    cd BlogArray/src
    ```

3. **Install Dependencies:**

    For the backend (ASP.NET Core):

    ```bash
    cd BlogArray
    dotnet restore
    ```

    For the frontend (Angular):

    ```bash
    cd BlogArray.Admin
    npm install
    ```

4. **Run the Application:**

    Start the backend server:

    ```bash
    cd BlogArray
    dotnet run
    ```

    Start the frontend server:

    ```bash
    cd BlogArray.Admin
    ng serve
    ```

5. **Open your browser and navigate to:**

    - Backend: `http://localhost:5000`
    - Frontend: `http://localhost:4200`

## Usage

After setting up the application, you can start creating and managing your content right away. Access the admin panel (at `https://yoursite.com/admin`) to configure your blog settings, manage users, and monitor analytics. 

For more detailed instructions on how to use each feature, refer to our [Documentation](#).

## Contribution

We welcome contributions from the community! To contribute to BlogArray:

1. **Fork the Repository** and clone it to your local machine.
2. **Create a New Branch** for your feature or fix:
   
    ```bash
    git checkout -b your-feature-branch
    ```

3. **Make Your Changes** and test thoroughly.
4. **Submit a Pull Request** detailing the changes you’ve made and why.

Please make sure to follow our [Contribution Guidelines](CONTRIBUTING.md) for detailed instructions.

## License

BlogArray is licensed under the [MIT License](LICENSE). Feel free to use, modify, and distribute it under the terms of this license.

---

Thank you for choosing BlogArray! We hope you enjoy using and contributing to this project. If you have any questions or need support, don't hesitate to reach out via [Issues](#) or [Discussions](#) on GitHub.

Happy blogging! 🚀

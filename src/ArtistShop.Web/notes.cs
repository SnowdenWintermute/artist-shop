// Users browse Paintings sorted by their various Series and other ShopItems such as PostCards,
// add and remove them from their Cart, proceed to CheckOut
// where they may provide their ShippingAddress, PaymentInformation,
// and create Orders while being charged a Payment.
//
// Users may check their Order's status, and their previous Orders in an OrderHistory.
//
// AdminUsers maintain, edit, and add new Paintings and their Series, setting details
// about them. AdminUsers update Orders' OrderStatus which trigger Notifications
// to users (via email)
//
// AdminUsers may see privledged information about Paintings such as
// when they were sold and to who, derived by the saved Orders.
//
// Desired features
// - view all series
// - view all paintings in a series
// - view other (non-unique) shop items
// - show "recently added" on the home/landing page
// - display a sort of "social media feed" or "blog post feed"
//   which has "blog posts" by the artist with arbitrary embedded
//   videos, images, and text
// - derive when paintings were sold and to who by the OrderHistory
//
// Artist
//
// ArtworkSeries
// - id
// - title
//
// ShopItem
// - stock
// - price
// - dimensions
//
// Painting extends ShopItem
// - id
// - title
// - image
// - thumbnail
// - datePainted
// - series
// Postcard extends ShopItem
// - image
// - thumbnail
//
// Cart
// - id
// - userId
//
// Checkout
// - id
// - userId
// - CheckoutStep
//
// Order
//
// User
// - id
// - role - standard, admin
//
//
// Pages
// - Landing
// - Painting Series
// - Shop (postcards, etc)
// - Videos
// - Storybook
// - About
